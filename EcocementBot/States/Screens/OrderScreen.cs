﻿using EcocementBot.Data.Enums;
using EcocementBot.Helpers;
using EcocementBot.Models;
using EcocementBot.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.Globalization;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using static EcocementBot.Models.OrderCarTime;

namespace EcocementBot.States.Screens;

public record struct Step
{
    public required Func<Chat, TelegramUser, Task> Ask { get; init; }

    public required Func<Message, Task> Handle { get; init; }
}

public class OrderScreen : IScreen
{
    public OrderState State { get; set; } = new();

    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly IServiceProvider _serviceProvider;
    private readonly OrderSender _sender;

    private const int MaxCars = 200;

    private static readonly CultureInfo s_culture = new CultureInfo("uk-UA");

    private readonly Dictionary<OrderStateType, Step> _steps;

    private static readonly ReplyKeyboardMarkup s_timeKeyboard = new()
    {
        Keyboard =
        [
            ["🌅 Ранок", "🌇 Обід", "🌃 Вечір"],
            ["🌤 Протягом дня"],
            ["🕘 Власний час"],
            [CommonButtons.PreviousStepButton]
        ]
    };

    private static readonly Dictionary<string, TimeOfDay> s_timeOfTheDayValues = new()
    {
        ["🌅 Ранок"] = TimeOfDay.Morning,
        ["🌇 Обід"] = TimeOfDay.Day,
        ["🌃 Вечір"] = TimeOfDay.Evening,
        ["🌤 Протягом дня"] = TimeOfDay.Anytime,
        ["🕘 Власний час"] = TimeOfDay.Custom,
    };

    private static readonly ReplyKeyboardMarkup s_payTypeKeyboard = new ReplyKeyboardMarkup()
    {
        Keyboard =
        [
            ["💵 Готівка", "🏦 Безготівка"],
            [CommonButtons.PreviousStepButton]
        ]
    };

    private static readonly Dictionary<string, OrderPaymentType> s_paymentTypeValues = new()
    {
        ["💵 Готівка"] = OrderPaymentType.Cash,
        ["🏦 Безготівка"] = OrderPaymentType.Cashless,
    };

    private static readonly Dictionary<string, OrderStateType> s_editStates = new()
    {
        ["📆 Змінити дату"] = OrderStateType.SelectDate,
        ["🤲 Змінити спосіб отримання"] = OrderStateType.SelectReceiveType,
        ["🔖 Змінити марку цементу"] = OrderStateType.SelectCementMark,
        ["🚚 Змінити кількість автомобілів і час доставки"] = OrderStateType.EnterCarsCount,
        ["💸 Змінити форму оплати"] = OrderStateType.SelectPaymentType,
        [CommonButtons.BackButton.Text] = OrderStateType.SelectFinalAction,
    };

    public OrderScreen(TelegramBotClient client,
        Navigator navigator,
        OrderSender sender,
        IServiceProvider serviceProvider)
    {
        _client = client;
        _navigator = navigator;
        _sender = sender;
        _serviceProvider = serviceProvider;

        _steps = new()
        {
            [OrderStateType.SelectDate] = new Step
            {
                Ask = (chat, _) =>
                {
                    var keyboard = new List<IEnumerable<KeyboardButton>>();

                    var date = DateTime.Now;
                    if (date.Hour >= 16)
                        date = date.AddDays(1);

                    for (int i = 0; i < 6; i++)
                    {
                        keyboard.Add([new KeyboardButton(date.ToString("dd.MM"))]);
                        date = date.AddDays(1);
                    }

                    if (State.IsEditMode)
                        keyboard.Add([CommonButtons.PreviousStepButton]);

                    return _client.SendMessage(chat, "Оберіть дату:",
                        replyMarkup: new ReplyKeyboardMarkup
                        {
                            Keyboard = keyboard,
                        });
                },
                Handle = async message =>
                {
                    var dateString = message.Text;
                    if (!DateTime.TryParseExact(dateString, "dd.MM", CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTime dateTime)
                        || dateTime < DateTime.Today
                        || (dateTime == DateTime.Today && DateTime.Now.Hour >= 16))
                    {
                        await _client.SendMessage(message.Chat, "❌ Хибна дата.");
                        await Ask(message);
                        return;
                    }
                    State.Date = DateOnly.FromDateTime(dateTime);
                    SetStateOrEdit(OrderStateType.SelectReceiveType);
                }
            },
            [OrderStateType.SelectReceiveType] = new Step()
            {
                Ask = (chat, _) =>
                {
                    return _client.SendMessage(chat, "Оберіть спосіб отримання:",
                        replyMarkup: new ReplyKeyboardMarkup
                        {
                            Keyboard =
                            [
                                ["🚚 Доставка"],
                                ["🏗 Самовивіз"],
                                [CommonButtons.PreviousStepButton]
                            ],
                        });
                },
                Handle = async message =>
                {
                    if (message.Text == "🚚 Доставка")
                        State.ReceiveType = OrderReceivingType.Delivery;
                    else if (message.Text == "🏗 Самовивіз")
                        State.ReceiveType = OrderReceivingType.SelfPickup;
                    else
                    {
                        await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
                        await Ask(message);
                        return;
                    }

                    SetStateOrEdit(OrderStateType.SelectCementMark);
                }
            },
            [OrderStateType.SelectCementMark] = new Step()
            {
                Ask = async (chat, _) =>
                {
                    await using var scoped = _serviceProvider.CreateAsyncScope();
                    var markService = scoped.ServiceProvider.GetRequiredService<MarkService>();

                    State.Marks = (await markService.GetMarks()).ToList();
                    var keyboard = KeyboardHelper.CreateKeyboard(State.Marks);
                    keyboard.Add([CommonButtons.PreviousStepButton]);
                    await _client.SendMessage(chat, "Оберіть марку цементу:",
                        replyMarkup: new ReplyKeyboardMarkup()
                        {
                            Keyboard = keyboard,
                        });
                },
                Handle = async message =>
                {
                    if (!State.Marks.Contains(message.Text!))
                    {
                        await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
                        await Ask(message);
                        return;
                    }

                    State.Mark = message.Text!;
                    SetStateOrEdit(OrderStateType.EnterCarsCount);
                }
            },
            [OrderStateType.EnterCarsCount] = new Step()
            {
                Ask = (chat, _) => _client.SendMessage(chat, "Введіть кількість авто:", replyMarkup: CommonButtons.PreviousStepButton),
                Handle = async message =>
                {
                    if (!int.TryParse(message.Text, out int carsCount) || carsCount <= 0 || carsCount > MaxCars)
                    {
                        await _client.SendMessage(message.Chat, "❌ Неправильне значення.");
                        await Ask(message);
                        return;
                    }

                    State.CarsCount = carsCount;
                    State.Type = OrderStateType.SelectCarTimeType;
                }
            },
            [OrderStateType.SelectCarTimeType] = new Step()
            {
                Ask = (chat, _) =>
                {
                    return _client.SendMessage(chat, "Оберіть час:",
                        replyMarkup: new ReplyKeyboardMarkup()
                        {
                            Keyboard =
                            [
                                ["🌤 Доставити протягом дня"],
                                ["⬅ Встановити індивідуально"],
                                [CommonButtons.PreviousStepButton]
                            ],
                        });
                },
                Handle = async message =>
                {
                    if (message.Text == "🌤 Доставити протягом дня")
                    {
                        State.OrderCarTime = new WithinDay();
                        await SelectPaymentType(message.From!);
                    }
                    else if (message.Text == "⬅ Встановити індивідуально")
                    {
                        State.OrderCarTime = new Individual();
                        State.Type = OrderStateType.SelectCar;
                    }
                    else
                    {
                        await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
                        await Ask(message);
                        return;
                    }
                }
            },
            [OrderStateType.SelectCar] = new Step()
            {
                Ask = (chat, _) =>
                {
                    State.Type = OrderStateType.SelectCar;
                    var keyboard = new List<IEnumerable<KeyboardButton>>();
                    var individual = (Individual)State.OrderCarTime!;

                    State.CarSelection.Clear();
                    for (int i = 0; i < State.CarsCount; i++)
                    {
                        if (i >= individual.CarTimes.Count)
                            State.CarSelection.Add($"🚚 Авто №{i + 1}", i);
                        else
                        {
                            var carTime = individual.CarTimes[i];
                            State.CarSelection.Add($"🚚 Авто №{i + 1} ({ToTimeOnly(carTime)}) ✅", i);
                        }
                    }

                    foreach (var car in State.CarSelection.Keys)
                        keyboard.Add([car]);

                    if (individual.CarTimes.Count == State.CarsCount)
                        keyboard.Add(["✅ Готово"]);

                    keyboard.Add([CommonButtons.PreviousStepButton]);

                    return _client.SendMessage(chat, $"Оберіть авто:", replyMarkup: new ReplyKeyboardMarkup()
                    {
                        Keyboard = keyboard
                    });
                },
                Handle = async message =>
                {
                    if (message.Text == "✅ Готово")
                    {
                        var individual = (Individual)State.OrderCarTime!;
                        if (individual.CarTimes.Count == State.CarsCount)
                        {
                            await SelectPaymentType(message.From!);
                            return;
                        }
                    }

                    if (!State.CarSelection.TryGetValue(message.Text!, out int carIndex))
                    {
                        await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
                        await Ask(message);
                        return;
                    }

                    State.CurrentCarIndex = carIndex;
                    State.Type = OrderStateType.SelectIndividualTime;
                }
            },
            [OrderStateType.SelectIndividualTime] = new Step()
            {
                Ask = (chat, _) => _client.SendMessage(chat, "Оберіть час:", replyMarkup: s_timeKeyboard),
                Handle = async message =>
                {
                    if (!s_timeOfTheDayValues.TryGetValue(message.Text!, out TimeOfDay timeOfDay))
                    {
                        await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
                        await Ask(message);
                        return;
                    }

                    var individual = (Individual)State.OrderCarTime!;
                    if (timeOfDay == TimeOfDay.Custom)
                    {
                        State.Type = OrderStateType.EnterIndividualCustomTime;
                        return;
                    }

                    SetCarTime(individual, new CarDeliveryTime(timeOfDay));
                    State.Type = OrderStateType.SelectCar;
                }
            },
            [OrderStateType.EnterIndividualCustomTime] = new Step()
            {
                Ask = AskEnterTime,
                Handle = async message =>
                {
                    if (!TimeOnly.TryParse(message.Text, out TimeOnly time))
                    {
                        await _client.SendMessage(message.Chat, "❌ Неправильний формат часу.");
                        await Ask(message);
                        return;
                    }

                    var individual = (Individual)State.OrderCarTime!;
                    SetCarTime(individual, new CarDeliveryTime(TimeOfDay.Custom, time));
                    State.Type = OrderStateType.SelectCar;
                }
            },
            [OrderStateType.SelectPaymentType] = new Step()
            {
                Ask = (chat, _) => _client.SendMessage(chat, $"Оберіть варіант оплати:", replyMarkup: s_payTypeKeyboard),
                Handle = async message =>
                {
                    if (message.Text is null)
                        return;

                    if (!s_paymentTypeValues.TryGetValue(message.Text, out OrderPaymentType paymentType))
                    {
                        await _client.SendMessage(message.Chat, "❌ Неправильний вибір.");
                        await Ask(message);
                        return;
                    }
                    State.PaymentType = paymentType;
                    SetStateOrEdit(OrderStateType.SelectFinalAction);
                }
            },
            [OrderStateType.SelectFinalAction] = new Step()
            {
                Ask = async (chat, user) =>
                {
                    await using var scoped = _serviceProvider.CreateAsyncScope();
                    var userService = scoped.ServiceProvider.GetRequiredService<UserService>();
                    var clientService = scoped.ServiceProvider.GetRequiredService<ClientService>();

                    var phoneNumber = await userService.GetPhoneNumber(user.Id);
                    var client = await clientService.GetClient(phoneNumber!);

                    StringBuilder builder = new();
                    builder.AppendLine($"Дата: {State.Date.ToString(s_culture)}");
                    builder.AppendLine($"Замовник: {client!.Name}");
                    builder.AppendLine($"Адреса: {client.Address}");

                    var receiveType = State.ReceiveType == OrderReceivingType.Delivery ? "🚚 Доставка" : "🏗 Самовивіз";
                    builder.AppendLine($"Спосіб отримання: {receiveType}");

                    string stringPaymentType = s_paymentTypeValues.First(p => p.Value == State.PaymentType).Key;
                    builder.AppendLine($"Форма оплати: {stringPaymentType}");

                    builder.AppendLine($"Марка цементу: {State.Mark}");

                    if (State.OrderCarTime is WithinDay)
                    {
                        builder.AppendLine($"Авто: доставимо протягом дня");
                        builder.AppendLine($"Кількість автівок: {State.CarsCount}");
                    }
                    else if (State.OrderCarTime is Individual individual)
                    {
                        builder.AppendLine();
                        for (int i = 0; i < individual.CarTimes.Count; i++)
                        {
                            CarDeliveryTime CarDeliveryTime = individual.CarTimes[i];
                            builder.AppendLine($"Авто №{i + 1}: {ToTimeOnly(CarDeliveryTime)}");
                        }
                    }

                    await _client.SendMessage(chat, builder.ToString(), replyMarkup: new ReplyKeyboardMarkup
                    {
                        Keyboard =
                        [
                            ["✅ Готово"],
                            ["✍️ Редагувати"],
                            ["🔄 Оформити замовлення наново"],
                        ]
                    });
                },
                Handle = async message =>
                {
                    switch (message.Text)
                    {
                        case "✅ Готово":
                            await _sender.Send(message.From!, new OrderModel
                            {
                                Date = State.Date,
                                Mark = State.Mark!,
                                CarsCount = State.CarsCount,
                                CarTime = State.OrderCarTime!,
                                PaymentType = State.PaymentType,
                                ReceiveType = State.ReceiveType,
                            });
                            State.Type = OrderStateType.Finish;
                            break;
                        case "✍️ Редагувати":
                            State.IsEditMode = true;
                            State.Type = OrderStateType.SelectEdit;
                            break;
                        case "🔄 Оформити замовлення наново":
                            State.Type = OrderStateType.SelectDate;
                            break;
                        default:
                            await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
                            await Ask(message);
                            break;
                    }
                }
            },
            [OrderStateType.SelectEdit] = new Step()
            {
                Ask = async (chat, user) =>
                {
                    await UpdatePaymentType(user);
                    await _client.SendMessage(chat, "Оберіть зміну:", replyMarkup: new ReplyKeyboardMarkup
                    {
                        Keyboard = s_editStates.Keys
                        .Where(k => k != "💸 Змінити форму оплати" || State.AllowSelectPaymentType)
                        .Select(k => new[] { new KeyboardButton(k) }).ToArray()
                    });
                },
                Handle = async message =>
                {
                    if (!s_editStates.TryGetValue(message.Text!, out OrderStateType stateType))
                    {
                        await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
                        await Ask(message);
                        return;
                    }

                    if (stateType == OrderStateType.SelectFinalAction)
                        State.IsEditMode = false;

                    State.Type = stateType;
                }
            },
            [OrderStateType.Finish] = new Step()
            {
                Ask = (chat, _) => _client.SendMessage(chat, "✅ Ваше замовлення в обробці.", replyMarkup: new ReplyKeyboardMarkup
                {
                    Keyboard =
                    [
                        ["➕ Нове замовлення"]
                    ]
                }),
                Handle = async message =>
                {
                    if (message.Text != "➕ Нове замовлення")
                    {
                        await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
                        await Ask(message);
                        return;
                    }

                    State.Type = OrderStateType.SelectDate;
                }
            }
        };
        
    }

    private void GoBack()
    {
        if (State.IsEditMode)
        {
            State.Type = State.Type switch
            {
                OrderStateType.SelectCarTimeType => OrderStateType.EnterCarsCount,
                OrderStateType.SelectCar => OrderStateType.SelectCarTimeType,
                OrderStateType.SelectIndividualTime => OrderStateType.SelectCar,
                OrderStateType.EnterIndividualCustomTime => OrderStateType.EnterIndividualCustomTime,
                _ => OrderStateType.SelectEdit,
            };
            return;
        }

        switch (State.Type)
        {
            case OrderStateType.SelectReceiveType:
                State.Type = OrderStateType.SelectDate;
                break;
            case OrderStateType.SelectCementMark:
                State.Type = OrderStateType.SelectReceiveType;
                break;
            case OrderStateType.EnterCarsCount:
                State.Type = OrderStateType.SelectCementMark;
                break;
            case OrderStateType.SelectCarTimeType:
                State.Type = OrderStateType.EnterCarsCount;
                break;
            case OrderStateType.SelectCar:
                State.Type = OrderStateType.SelectCarTimeType;
                break;
            case OrderStateType.SelectIndividualTime:
                State.Type = OrderStateType.SelectCar;
                break;
            case OrderStateType.EnterIndividualCustomTime:
                State.Type = OrderStateType.EnterIndividualCustomTime;
                break;
            case OrderStateType.SelectPaymentType:
                State.Type = State.OrderCarTime switch
                {
                    WithinDay => OrderStateType.SelectCarTimeType,
                    Individual => OrderStateType.SelectCar,
                };
                break;
        }
    }


    private void SetCarTime(Individual individual, CarDeliveryTime carTime)
    {
        if (State.CurrentCarIndex >= individual.CarTimes.Count)
            individual.CarTimes.Add(carTime);
        else
            individual.CarTimes[State.CurrentCarIndex] = carTime;
    }

    private async Task SelectPaymentType(User user)
    {
        await UpdatePaymentType(user);

        if (State.AllowSelectPaymentType)
            SetStateOrEdit(OrderStateType.SelectPaymentType);
        else
            SetStateOrEdit(OrderStateType.SelectFinalAction);
    }

    private async Task UpdatePaymentType(User user)
    {
        await using var scoped = _serviceProvider.CreateAsyncScope();
        var userService = scoped.ServiceProvider.GetRequiredService<UserService>();
        var clientService = scoped.ServiceProvider.GetRequiredService<ClientService>();

        var phone = await userService.GetPhoneNumber(user.Id);
        var client = await clientService.GetClient(phone!);

        State.AllowSelectPaymentType = client.PaymentType == ClientPaymentType.Both;
        if(!State.AllowSelectPaymentType)
            State.PaymentType = (OrderPaymentType)client.PaymentType;
    }

    public Task EnterAsync(TelegramUser telegramUser, Chat chat) => Ask(chat, telegramUser);

    public async Task HandleInput(Message message)
    {
        if (message.Text is null)
            return;

        if (message.Text == CommonButtons.PreviousStepButton.Text
            && (State.Type != OrderStateType.SelectDate || State.IsEditMode)
            && State.Type != OrderStateType.SelectEdit)
        {
            GoBack();
            await Ask(message);
            return;
        }

        OrderStateType stateTypeBefore = State.Type;

        await _steps[State.Type].Handle(message);

        if (stateTypeBefore != State.Type)
            await Ask(message);
    }

    private Task Ask(Message message) => Ask(message.Chat, message.From!);
    private Task Ask(Chat chat, User user) => _steps[State.Type].Ask(chat, user);

    private void SetStateOrEdit(OrderStateType orderStateType)
    {
        if (State.IsEditMode)
            State.Type = OrderStateType.SelectEdit;
        else
            State.Type = orderStateType;
    }

    private async Task AskEnterTime(Chat chat, TelegramUser user)
    {
        await _client.SendMessage(chat, "❗ Ми намагатимемося доставити цемент у вказаний вами час, " +
                            "однак точна доставка не гарантується, оскільки вона залежить від кількості замовлень на цей період " +
                            "та поточної завантаженості водіїв.");
        await _client.SendMessage(chat, "Введіть час:", replyMarkup: CommonButtons.PreviousStepButton);
    }

    private static string ToTimeOnly(CarDeliveryTime CarDeliveryTime)
    {
        if (CarDeliveryTime.TimeOfDay == TimeOfDay.Custom)
            return CarDeliveryTime.CustomTime!.Value.ToString(s_culture)!;

        return s_timeOfTheDayValues.First(i => i.Value == CarDeliveryTime.TimeOfDay).Key;
    }

    public class OrderState
    {
        public OrderStateType Type { get; set; } = OrderStateType.SelectDate;

        public DateOnly Date { get; set; }

        public OrderReceivingType ReceiveType { get; set; }

        public string? Mark { get; set; }

        public int CarsCount { get; set; }

        public OrderCarTime? OrderCarTime { get; set; }

        public TimeOfDay CurrentTimeOfDay { get; set; }

        public int CurrentCarIndex { get; set; }

        public Dictionary<string, int> CarSelection { get; set; } = [];

        public OrderPaymentType PaymentType { get; set; }

        public bool IsEditMode { get; set; }

        public List<string> Marks { get; set; } = [];

        public bool AllowSelectPaymentType { get; set; }
    }

    public enum OrderStateType
    {
        SelectDate,
        SelectReceiveType,
        SelectCementMark,
        EnterCarsCount,
        SelectCarTimeType,
        SelectCar,
        SelectIndividualTime,
        EnterIndividualCustomTime,
        SelectPaymentType,
        SelectFinalAction,
        SelectEdit,
        Finish
    }
}

