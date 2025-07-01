using EcocementBot.Data.Enums;
using EcocementBot.Helpers;
using EcocementBot.Models;
using EcocementBot.Services;
using System;
using System.Globalization;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Clients;

public class OrderScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly MarkService _markService;
    private readonly ClientService _clientService;
    private readonly SessionService _sessionService;
    private readonly OrderSender _sender;

    private readonly OrderState _state = new();

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
    }

    public Task EnterAsync(TelegramUser user, Chat chat)
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
    }

    public async Task HandleInput(Message message)
    {
        switch (_state.Type)
        {
            case OrderStateType.SelectDate:
                var dateString = message.Text;
                if (!DateTime.TryParseExact(dateString, "dd.MM", CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out DateTime dateTime))
                {
                    await _client.SendMessage(message.Chat, "✖️ Хибна дата.");
                    break;
                }
                _state.Date = DateOnly.FromDateTime(dateTime);
                await _client.SendMessage(message.Chat, "Оберіть спосіб доставки:",
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
                _state.Type = OrderStateType.SelectReceiveType;
                break;
            case OrderStateType.SelectReceiveType:
                if (message.Text == "🚚 Доставка")
                    _state.ReceiveType = OrderReceivingType.Delivery;
                else if (message.Text == "🏗 Самовивіз")
                    _state.ReceiveType = OrderReceivingType.SelfPickup;
                else
                {
                    await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                    break;
                }

                IEnumerable<string> marks = await _markService.GetMarks();
                await _client.SendMessage(message.Chat, "Оберіть марку цементу:",
                    replyMarkup: new ReplyKeyboardMarkup()
                    {
                        Keyboard = KeyboardHelper.CreateKeyboard(marks.ToArray()),
                    });

                _state.Type = OrderStateType.SelectCementMark;
                break;
            case OrderStateType.SelectCementMark:
                _state.Mark = message.Text;
                await _client.SendMessage(message.Chat, "Введіть кількість авто:", replyMarkup: new ReplyKeyboardRemove());
                _state.Type = OrderStateType.EnterCarsCount;
                break;
            case OrderStateType.EnterCarsCount:
                if (!int.TryParse(message.Text, out int carsCount) || carsCount <= 0)
                {
                    await _client.SendMessage(message.Chat, "✖️ Неправильне значення.");
                    break;
                }
                _state.CarsCount = carsCount;

                if (carsCount == 1)
                {
                    await AskSelectGeneralTime(message);
                    break;
                }

                await _client.SendMessage(message.Chat, "Оберіть час:",
                        replyMarkup: new ReplyKeyboardMarkup()
                        {
                            Keyboard =
                            [
                                [new KeyboardButton("🕒 Один час для всіх авто")],
                                [new KeyboardButton("⬅ Встановити індивідуально")],
                            ],
                        });


                _state.Type = OrderStateType.SelectCarDeliveryType;
                break;
            case OrderStateType.SelectCarDeliveryType:
                if (_state.CarsCount > 1)
                {
                    if (message.Text == "🕒 Один час для всіх авто")
                        await AskSelectGeneralTime(message);
                    else if (message.Text == "⬅ Встановити індивідуально")
                    {
                        _state.Type = OrderStateType.SelectIndividualTime;
                        var individual2 = new OrderCarTime.Individual();
                        _state.OrderCarTime = individual2;

                        for (int i = individual2.CarTimes.Count; i < _state.CarsCount; i++)
                            _state.CarSelection.Add($"🚚 Авто №{i + 1}", i);

                        await AskSelectCar(message.Chat);
                    }
                    else
                    {
                        await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                        break;
                    }
                }

                break;
            case OrderStateType.SelectGeneralTime:
                if (!s_timeOfTheDayValues.TryGetValue(message.Text, out TimeOfDay timeOfDay))
                {
                    await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                    break;
                }

                if (timeOfDay != TimeOfDay.Custom)
                {
                    _state.OrderCarTime = new OrderCarTime.General(new CarDeliveryTime(timeOfDay));
                    await AskPaymentType(message.Chat);
                    break;
                }

                await AskEnterTime(message);
                _state.Type = OrderStateType.EnterGeneralCustomTime;
                break;
            case OrderStateType.EnterGeneralCustomTime:
                if (!TimeOnly.TryParse(message.Text, out TimeOnly time))
                {
                    await _client.SendMessage(message.Chat, "✖️ Неправильний формат часу.");
                    break;
                }

                _state.OrderCarTime = new OrderCarTime.General(new CarDeliveryTime(_state.CurrentTimeOfDay, time));
                await AskPaymentType(message.Chat);
                break;
            case OrderStateType.SelectCar:
                if (!_state.CarSelection.TryGetValue(message.Text, out int carIndex))
                {
                    await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                    break;
                }
                _state.CurrentCarIndex = carIndex;
                await _client.SendMessage(message.Chat, "Оберіть час:", replyMarkup: s_timeKeyboard);
                _state.Type = OrderStateType.SelectIndividualTime;
                break;
            case OrderStateType.SelectIndividualTime:
                if (!s_timeOfTheDayValues.TryGetValue(message.Text, out TimeOfDay timeOfDay2))
                {
                    await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                    break;
                }

                var individual = (OrderCarTime.Individual)_state.OrderCarTime!;
                if (timeOfDay2 == TimeOfDay.Custom)
                {
                    await AskEnterTime(message);
                    _state.Type = OrderStateType.EnterIndividualCustomTime;
                    break;
                }

                individual.CarTimes.Add(new CarDeliveryTime(timeOfDay2));
                RemoveCarSelection();
                await FinishSettingCars(individual, message.Chat);
                break;
            case OrderStateType.EnterIndividualCustomTime:
                if (!TimeOnly.TryParse(message.Text, out TimeOnly time2))
                {
                    await _client.SendMessage(message.Chat, "✖️ Неправильний формат часу.");
                    break;
                }

                individual = (OrderCarTime.Individual)_state.OrderCarTime!;
                individual.CarTimes.Add(new CarDeliveryTime(TimeOfDay.Custom, time2));
                RemoveCarSelection();

                await FinishSettingCars(individual, message.Chat);
                break;
            case OrderStateType.SelectPaymentType:
                if(!s_paymentTypeValues.TryGetValue(message.Text, out PaymentType paymentType))
                { 
                    await _client.SendMessage(message.Chat, "✖️ Неправильний вибір.");
                    break;
                }
                _state.PaymentType = paymentType;

                await SendSummary(message);
                _state.Type = OrderStateType.SelectFinalAction;
                break;
            case OrderStateType.SelectFinalAction:
                if (message.Text == "✅ Зберегти")
                {
                    await _sender.Send(message.From!, new OrderModel
                    {
                        Date = _state.Date,
                        CarsCount = _state.CarsCount,
                        Mark = _state.Mark,
                        CarTime = _state.OrderCarTime!,
                        PaymentType = _state.PaymentType,
                        ReceiveType = _state.ReceiveType,
                    });
                    await _client.SendMessage(message.Chat, "✅ Ваше замовлення в обробці.");
                }
                break;
        }
    }

    private async Task AskEnterTime(Message message)
    {
        await _client.SendMessage(message.Chat, "❗ Ми намагатимемося доставити цемент у вказаний вами час, " +
                            "однак точна доставка не гарантується, оскільки вона залежить від кількості замовлень на цей період " +
                            "та поточної завантаженості водіїв.");
        await _client.SendMessage(message.Chat, "Введіть час:", replyMarkup: new ReplyKeyboardRemove());
    }

    private async Task AskSelectGeneralTime(Message message)
    {
        _state.Type = OrderStateType.SelectGeneralTime;
        await _client.SendMessage(message.Chat, "Оберіть час:", replyMarkup: s_timeKeyboard);
    }

    private async Task SendSummary(Message message)
    {
        var phoneNumber = _sessionService.GetPhoneNumber(message.From!.Id);
        var client = await _clientService.GetClient(phoneNumber);

        StringBuilder builder = new();
        builder.AppendLine($"Дата: {_state.Date}");
        builder.AppendLine($"Замовник: {client!.Name}");
        builder.AppendLine($"Адреса: {client.Address}");
        string stringPaymentType = s_paymentTypeValues.First(p => p.Value == _state.PaymentType).Key;
        builder.AppendLine($"Форма оплати: {stringPaymentType}");
        builder.AppendLine($"Марка цементу: {_state.Mark}"); ;
        if(_state.OrderCarTime is OrderCarTime.General general)
        {
            builder.AppendLine($"Кількість автівок: {_state.CarsCount}");
            builder.AppendLine($"Загальний час автівок: {ToTimeOnly(general.Time)}");
        }
        else if(_state.OrderCarTime is OrderCarTime.Individual individual)
        {
            builder.AppendLine();
            for (int i = 0; i < individual.CarTimes.Count; i++)
            {
                CarDeliveryTime CarDeliveryTime = individual.CarTimes[i];
                builder.AppendLine($"Авто №{i+1}: {ToTimeOnly(CarDeliveryTime)}");
            }
        }
       

        await _client.SendMessage(message.Chat, builder.ToString(), replyMarkup: new ReplyKeyboardMarkup
        {
            Keyboard = 
            [
                [new KeyboardButton("✍️ Редагувати")],
                [new KeyboardButton("✅ Зберегти")],
            ]
        });
    }

    private string ToTimeOnly(CarDeliveryTime CarDeliveryTime)
    {
        if (CarDeliveryTime.TimeOfDay == TimeOfDay.Custom)
            return CarDeliveryTime.CustomTime!.Value.ToString(CultureInfo.CurrentUICulture)!;

        return s_timeOfTheDayValues.First(i => i.Value == CarDeliveryTime.TimeOfDay).Key;
    }

    private void RemoveCarSelection()
    {
        var key = _state.CarSelection.First(v => v.Value == _state.CurrentCarIndex).Key;
        _state.CarSelection.Remove(key);
    }

    private Task<Message> FinishSettingCars(OrderCarTime.Individual individual, Chat chat)
    {
        if (individual.CarTimes.Count == _state.CarsCount)
            return AskPaymentType(chat);

        return AskSelectCar(chat);
    }

    private Task<Message> AskPaymentType(Chat chat)
    {
        _state.Type = OrderStateType.SelectPaymentType;
        return _client.SendMessage(chat, $"Оберіть варіант оплати:", replyMarkup: s_payTypeKeyboard);
    }

    private Task<Message> AskSelectCar(Chat chat)
    {
        _state.Type = OrderStateType.SelectCar;
        var keyboard = new List<IEnumerable<KeyboardButton>>();
        foreach (string selection in _state.CarSelection.Keys)
            keyboard.Add([selection]);

        return _client.SendMessage(chat, $"Оберіть авто:", replyMarkup: new ReplyKeyboardMarkup()
        {
            Keyboard = keyboard
        });
    }

    private class OrderState
    {
        public OrderStateType Type { get; set; } = OrderStateType.SelectDate;

        public DateOnly Date { get; set; }

        public OrderReceivingType ReceiveType { get; set; }

        public string Mark { get; set; }

        public int CarsCount { get; set; }

        public OrderCarTime? OrderCarTime { get; set; }

        public TimeOfDay CurrentTimeOfDay { get; set; }

        public int CurrentCarIndex { get; set; }

        public Dictionary<string, int> CarSelection { get; } = [];

        public PaymentType PaymentType { get; set; }
    }

    private enum OrderStateType
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
        SelectPayFormat,
        SelectPaymentType,
        SelectFinalAction,
    }
}

