using EcocementBot.Data.Enums;
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
    private readonly MarkService _markService;
    private readonly ClientService _clientService;
    private readonly SessionService _sessionService;
    private readonly OrderSender _sender;
    private readonly CultureInfo _culture = new CultureInfo("uk-UA");

    private readonly Dictionary<OrderStateType, Step> _steps;

    private static readonly ReplyKeyboardMarkup s_timeKeyboard = new()
    {
        Keyboard =
        [
            [
                new KeyboardButton("🌅 Ранок"),
                new KeyboardButton("🌇 Обід"),
                new KeyboardButton("🌃 Вечір"),
            ],
            [new KeyboardButton("🕘 Власний час")]
        ]
    };

    private static readonly ReplyKeyboardMarkup s_payTypeKeyboard = new ReplyKeyboardMarkup()
    {
        Keyboard =
        [
            [
                new KeyboardButton("💵 Готівка"),
                new KeyboardButton("🏦 Безготівка")
            ]
        ]
    };

    private static readonly Dictionary<string, TimeOfDay> s_timeOfTheDayValues = new()
    {
        ["🌅 Ранок"] = TimeOfDay.Morning,
        ["🌇 Обід"] = TimeOfDay.Day,
        ["🌃 Вечір"] = TimeOfDay.Evening,
        ["🕘 Власний час"] = TimeOfDay.Custom,
    };

    private static readonly Dictionary<string, PaymentType> s_paymentTypeValues = new()
    {
        ["💵 Готівка"] = PaymentType.Cash,
        ["🏦 Безготівка"] = PaymentType.Card,
    };

    private static readonly Dictionary<string, OrderStateType> s_editStates = new()
    {
        ["📆 Змінити дату"] = OrderStateType.SelectDate,
        ["🤲 Змінити спосіб отримання"] = OrderStateType.SelectReceiveType,
        ["🔖 Змінити марку цементу"] = OrderStateType.SelectCementMark,
        ["🚚 Змінити кількість автомобілів і час доставки"] = OrderStateType.EnterCarsCount,
        ["💸 Змінити форму оплати"] = OrderStateType.SelectPaymentType,
        ["⬅️ Назад"] = OrderStateType.SelectFinalAction,
    };

    public OrderScreen(TelegramBotClient client,
        Navigator navigator,
        MarkService markService,
        ClientService clientService,
        SessionService sessionService,
        IConfiguration configuration,
        OrderSender sender)
    {
        _client = client;
        _navigator = navigator;
        _markService = markService;
        _clientService = clientService;
        _sessionService = sessionService;
        _sender = sender;

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

                    return _client.SendMessage(chat, "Оберіть дату:",
                        replyMarkup: new ReplyKeyboardMarkup
                        {
                            Keyboard = keyboard,
                        });
                },
                Handle = async message =>
                {
                    var dateString = message.Text;
                    if (!DateTime.TryParseExact(dateString, "dd.MM", CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTime dateTime))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Хибна дата.");
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
                    return _client.SendMessage(chat, "Оберіть спосіб доставки:",
                        replyMarkup: new ReplyKeyboardMarkup
                        {
                            Keyboard =
                            [
                                [
                                new KeyboardButton("🚚 Доставка"),
                                new KeyboardButton("🏗 Самовивіз"),
                            ]
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
                        await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                        return;
                    }

                    SetStateOrEdit(OrderStateType.SelectCementMark);
                }
            },
            [OrderStateType.SelectCementMark] = new Step()
            {
                Ask = async (chat, _) =>
                {
                    State.Marks = (await _markService.GetMarks()).ToList();
                    await _client.SendMessage(chat, "Оберіть марку цементу:",
                        replyMarkup: new ReplyKeyboardMarkup()
                        {
                            Keyboard = KeyboardHelper.CreateKeyboard(State.Marks),
                        });
                },
                Handle = message =>
                {
                    if (!State.Marks.Contains(message.Text!))
                        return _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");

                    State.Mark = message.Text!;
                    SetStateOrEdit(OrderStateType.EnterCarsCount);
                    return Task.CompletedTask;
                }
            },
            [OrderStateType.EnterCarsCount] = new Step()
            {
                Ask = (chat, _) => _client.SendMessage(chat, "Введіть кількість авто:", replyMarkup: new ReplyKeyboardRemove()),
                Handle = async message =>
                {
                    if (!int.TryParse(message.Text, out int carsCount) || carsCount <= 0)
                    {
                        await _client.SendMessage(message.Chat, "✖️ Неправильне значення.");
                        return;
                    }

                    State.CarsCount = carsCount;

                    if (carsCount == 1)
                    {
                        State.Type = OrderStateType.SelectGeneralTime;
                        return;
                    }
                    State.Type = OrderStateType.SelectCarDeliveryType;
                }
            },
            [OrderStateType.SelectCarDeliveryType] = new Step()
            {
                Ask = (chat, _) =>
                {
                    return _client.SendMessage(chat, "Оберіть час:",
                        replyMarkup: new ReplyKeyboardMarkup()
                        {
                            Keyboard =
                            [
                                [new KeyboardButton("🕒 Один час для всіх авто")],
                                [new KeyboardButton("⬅ Встановити індивідуально")],
                            ],
                        });
                },
                Handle = async message =>
                {
                    if (message.Text == "🕒 Один час для всіх авто")
                        State.Type = OrderStateType.SelectGeneralTime;
                    else if (message.Text == "⬅ Встановити індивідуально")
                    {
                        State.Type = OrderStateType.SelectIndividualTime;
                        var individual2 = new Individual();
                        State.OrderCarTime = individual2;

                        for (int i = individual2.CarTimes.Count; i < State.CarsCount; i++)
                            State.CarSelection.Add($"🚚 Авто №{i + 1}", i);

                        State.Type = OrderStateType.SelectCar;
                    }
                    else
                    {
                        await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                        return;
                    }
                }
            },
            [OrderStateType.SelectGeneralTime] = new Step()
            {
                Ask = (chat, _) => _client.SendMessage(chat, "Оберіть час:", replyMarkup: s_timeKeyboard),
                Handle = async message =>
                {
                    if (!s_timeOfTheDayValues.TryGetValue(message.Text!, out TimeOfDay timeOfDay))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                        return;
                    }

                    if (timeOfDay != TimeOfDay.Custom)
                    {
                        State.OrderCarTime = new General(new CarDeliveryTime(timeOfDay));
                        SetStateOrEdit(OrderStateType.SelectPaymentType);
                        return;
                    }

                    State.Type = OrderStateType.EnterGeneralCustomTime;
                }
            },
            [OrderStateType.EnterGeneralCustomTime] = new Step()
            {
                Ask = AskEnterTime,
                Handle = async message =>
                {
                    if (!TimeOnly.TryParse(message.Text, out TimeOnly time))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Неправильний формат часу.");
                        return;
                    }

                    State.OrderCarTime = new General(new CarDeliveryTime(State.CurrentTimeOfDay, time));
                    SetStateOrEdit(OrderStateType.SelectPaymentType);
                }
            },
            [OrderStateType.SelectCar] = new Step()
            {
                Ask = (chat, _) =>
                {
                    State.Type = OrderStateType.SelectCar;
                    var keyboard = new List<IEnumerable<KeyboardButton>>();
                    foreach (string selection in State.CarSelection.Keys)
                        keyboard.Add([selection]);

                    return _client.SendMessage(chat, $"Оберіть авто:", replyMarkup: new ReplyKeyboardMarkup()
                    {
                        Keyboard = keyboard
                    });
                },
                Handle = async message =>
                {
                    if (!State.CarSelection.TryGetValue(message.Text!, out int carIndex))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
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
                    if (!s_timeOfTheDayValues.TryGetValue(message.Text!, out TimeOfDay timeOfDay2))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                        return;
                    }

                    var individual = (Individual)State.OrderCarTime!;
                    if (timeOfDay2 == TimeOfDay.Custom)
                    {
                        State.Type = OrderStateType.EnterIndividualCustomTime;
                        return;
                    }

                    individual.CarTimes.Add(new CarDeliveryTime(timeOfDay2));
                    RemoveCarSelection();
                    FinishSettingCars(individual);
                }
            },
            [OrderStateType.EnterIndividualCustomTime] = new Step()
            {
                Ask = AskEnterTime,
                Handle = async message =>
                {
                    if (!TimeOnly.TryParse(message.Text, out TimeOnly time2))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Неправильний формат часу.");
                        return;
                    }

                    var individual = (Individual)State.OrderCarTime!;
                    individual.CarTimes.Add(new CarDeliveryTime(TimeOfDay.Custom, time2));
                    RemoveCarSelection();
                    FinishSettingCars(individual);
                }
            },
            [OrderStateType.SelectPaymentType] = new Step()
            {
                Ask = (chat, _) => _client.SendMessage(chat, $"Оберіть варіант оплати:", replyMarkup: s_payTypeKeyboard),
                Handle = async message =>
                {
                    if (message.Text is null)
                        return;

                    if (!s_paymentTypeValues.TryGetValue(message.Text, out PaymentType paymentType))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Неправильний вибір.");
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
                    var phoneNumber = _sessionService.GetPhoneNumber(user.Id);
                    var client = await _clientService.GetClient(phoneNumber);

                    StringBuilder builder = new();
                    builder.AppendLine($"Дата: {State.Date.ToString(_culture)}");
                    builder.AppendLine($"Замовник: {client!.Name}");
                    builder.AppendLine($"Адреса: {client.Address}");
                    string stringPaymentType = s_paymentTypeValues.First(p => p.Value == State.PaymentType).Key;
                    builder.AppendLine($"Форма оплати: {stringPaymentType}");
                    builder.AppendLine($"Марка цементу: {State.Mark}"); ;
                    if (State.OrderCarTime is General general)
                    {
                        builder.AppendLine($"Кількість автівок: {State.CarsCount}");
                        builder.AppendLine($"Загальний час автівок: {ToTimeOnly(general.Time)}");
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
                            [new KeyboardButton("✍️ Редагувати")],
                            [new KeyboardButton("✅ Готово")],
                        ]
                    });
                },
                Handle = async message => 
                {
                    if (message.Text == "✅ Готово")
                    {
                        await _sender.Send(message.From!, new OrderModel
                        {
                            Date = State.Date,
                            CarsCount = State.CarsCount,
                            Mark = State.Mark!,
                            CarTime = State.OrderCarTime!,
                            PaymentType = State.PaymentType,
                            ReceiveType = State.ReceiveType,
                        });
                        
                        State.Type = OrderStateType.Finish;
                    }
                    else if(message.Text == "✍️ Редагувати")
                    {
                        State.IsEditMode = true;
                        State.Type = OrderStateType.SelectEdit;
                    }
                    else
                        await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                }
            },
            [OrderStateType.SelectEdit] = new Step()
            {
                Ask = (chat, user) => _client.SendMessage(chat, "Оберіть зміну:", replyMarkup: new ReplyKeyboardMarkup
                {
                    Keyboard = s_editStates.Keys.Select(k => new[] { new KeyboardButton(k) }).ToArray()
                }),
                Handle = async message =>
                {


                    if (!s_editStates.TryGetValue(message.Text!, out OrderStateType stateType))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                        return;
                    }
                    State.Type = stateType;
                }
            },
            [OrderStateType.Finish] = new Step()
            { 
                Ask = (chat, _) => _client.SendMessage(chat, "✅ Ваше замовлення в обробці.", replyMarkup: new ReplyKeyboardMarkup
                {
                    Keyboard = 
                    [
                        [new KeyboardButton("➕ Нове замовлення")]
                    ]
                }),
                Handle = message =>
                {
                    if (message.Text == "➕ Нове замовлення")
                        State.Type = OrderStateType.SelectDate;

                    return Task.CompletedTask;
                }
            }
        };
    }

    public Task EnterAsync(TelegramUser user, Chat chat)
    {
        return _steps[State.Type].Ask(chat, user);
    }

    public async Task HandleInput(Message message)
    {
        if (message.Text is null)
            return;

        OrderStateType stateTypeBefore = State.Type;

        await _steps[State.Type].Handle(message);

        if (stateTypeBefore != State.Type)
            await _steps[State.Type].Ask(message.Chat, message.From!);
    }

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
        await _client.SendMessage(chat, "Введіть час:", replyMarkup: new ReplyKeyboardRemove());
    }

    private static string ToTimeOnly(CarDeliveryTime CarDeliveryTime)
    {
        if (CarDeliveryTime.TimeOfDay == TimeOfDay.Custom)
            return CarDeliveryTime.CustomTime!.Value.ToString(CultureInfo.CurrentUICulture)!;

        return s_timeOfTheDayValues.First(i => i.Value == CarDeliveryTime.TimeOfDay).Key;
    }

    private void RemoveCarSelection()
    {
        var key = State.CarSelection.First(v => v.Value == State.CurrentCarIndex).Key;
        State.CarSelection.Remove(key);
    }

    private void FinishSettingCars(Individual individual)
    {
        if (individual.CarTimes.Count == State.CarsCount)
            SetStateOrEdit(OrderStateType.SelectPaymentType);
        else
            State.Type = OrderStateType.SelectCar;
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

        public PaymentType PaymentType { get; set; }

        public bool IsEditMode { get; set; }

        public List<string> Marks { get; set; } = [];
    }

    public enum OrderStateType
    {
        SelectDate,
        SelectReceiveType,
        SelectCementMark,
        EnterCarsCount,
        SelectCarDeliveryType,
        SelectGeneralTime,
        EnterGeneralCustomTime,
        SelectCar,
        SelectIndividualTime,
        EnterIndividualCustomTime,
        SelectPaymentType,
        SelectFinalAction,
        SelectEdit,
        Finish
    }
}

