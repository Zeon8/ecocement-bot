using EcocementBot.Helpers;
using EcocementBot.Services;
using System;
using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Clients;

public class OrderScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly MarkService _markService;

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

    private static readonly Dictionary<string, TimeOfDay> s_timeOfTheDayValues = new()
    {
        ["🌅 Ранок"] = TimeOfDay.Morning,
        ["🌇 Обід"] = TimeOfDay.Day,
        ["🌃 Вечір"] = TimeOfDay.Evening,
        ["🕘 Власний час"] = TimeOfDay.Custom,
    };

    public OrderScreen(TelegramBotClient client, Navigator navigator, MarkService markService)
    {
        _client = client;
        _navigator = navigator;
        _markService = markService;
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
                    _state.ReceiveType = ReceivingType.Delivery;
                else if (message.Text == "🏗 Самовивіз")
                    _state.ReceiveType = ReceivingType.SelfPickup;
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

                if (carsCount > 1)
                {
                    await _client.SendMessage(message.Chat, "Оберіть час:",
                        replyMarkup: new ReplyKeyboardMarkup()
                        {
                            Keyboard =
                            [
                                [new KeyboardButton("🕒 Один час для всіх авто")],
                            [new KeyboardButton("⬅ Встановити індивідуально")],

                            ],
                        });
                }

                _state.Type = OrderStateType.SelectCarDeliveryType;
                break;
            case OrderStateType.SelectCarDeliveryType:
                if (_state.CarsCount > 1)
                {
                    if (message.Text == "🕒 Один час для всіх авто")
                        _state.Type = OrderStateType.SelectGeneralTime;
                    else if (message.Text == "⬅ Встановити індивідуально")
                        _state.Type = OrderStateType.SelectIndividualTime;
                    else
                    {
                        await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                        break;
                    }
                }

                if (_state.Type == OrderStateType.SelectIndividualTime)
                {
                    _state.Type = OrderStateType.SelectIndividualTime;
                    await _client.SendMessage(message.Chat, "🚚 Для авто №1:");
                }
                else
                    _state.Type = OrderStateType.SelectGeneralTime;

                await _client.SendMessage(message.Chat, "Оберіть час:", replyMarkup: s_timeKeyboard);
                break;
            case OrderStateType.SelectGeneralTime:
                if (!s_timeOfTheDayValues.TryGetValue(message.Text, out TimeOfDay timeOfDay))
                {
                    await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                    break;
                }

                if (timeOfDay != TimeOfDay.Custom)
                {
                    _state.CarTimeSetup = new CarTimeSetup.General(new DeliveryTime(timeOfDay));
                    break;
                }

                await _client.SendMessage(message.Chat, "Введіть час:");
                _state.Type = OrderStateType.EnterGeneralCustomTime;
                break;
            case OrderStateType.EnterGeneralCustomTime:
                if (!TimeOnly.TryParse(message.Text, out TimeOnly time))
                {
                    await _client.SendMessage(message.Chat, "✖️ Неправильний формат часу.");
                    break;
                }

                _state.CarTimeSetup = new CarTimeSetup.General(new DeliveryTime(_state.CurrentTimeOfDay, time));
                break;
            case OrderStateType.SelectIndividualTime:
                if (!s_timeOfTheDayValues.TryGetValue(message.Text, out TimeOfDay timeOfDay2))
                {
                    await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                    break;
                }

                var individual = (CarTimeSetup.Individual)_state.CarTimeSetup!;
                if (timeOfDay2 != TimeOfDay.Custom)
                {
                    individual.CarTimes.Add(new DeliveryTime(timeOfDay2));
                    break;
                }

                await _client.SendMessage(message.Chat, "Введіть час:");
                _state.Type = OrderStateType.EnterIndividualCustomTime;
                break;
            case OrderStateType.EnterIndividualCustomTime:
                if (!TimeOnly.TryParse(message.Text, out TimeOnly time2))
                {
                    await _client.SendMessage(message.Chat, "✖️ Неправильний формат часу.");
                    break;
                }

                individual = (CarTimeSetup.Individual)_state.CarTimeSetup!;
                individual.CarTimes.Add(new DeliveryTime(TimeOfDay.Custom, time2));
                if(individual.CarTimes.Count == _state.CarsCount)
                {

                }
                break;
            case OrderStateType.SelectPayFormat:
        }
    }

    private Task AskSelectTime(Chat chat, DeliveryTime deliveryTime)
    {
        if (deliveryTime.TimeOfDay == TimeOfDay.Custom)
            return _client.SendMessage(chat, "Оберіть час:", replyMarkup: new ReplyKeyboardRemove());

        var keyboard =
        return _client.SendMessage(chat, "Оберіть час:", replyMarkup: keyboard);
    }

    private class OrderState
    {
        public OrderStateType Type { get; set; } = OrderStateType.SelectDate;

        public DateOnly Date { get; set; }

        public ReceivingType ReceiveType { get; set; }

        public string Mark { get; set; }

        public int CarsCount { get; set; }

        public CarTimeSetup? CarTimeSetup { get; set; }

        public TimeOfDay CurrentTimeOfDay { get; set; }
    }

    private enum TimeOfDay
    {
        Morning,
        Day,
        Evening,
        Custom
    }

    private record DeliveryTime(TimeOfDay TimeOfDay, TimeOnly? CustomTime = null);

    private abstract record CarTimeSetup
    {
        public record General(DeliveryTime Time) : CarTimeSetup;

        public record Individual : CarTimeSetup
        {
            public List<DeliveryTime> CarTimes { get; } = new();
        }
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
        SelectIndividualTime,
        EnterIndividualCustomTime,
        SelectPayFormat,
    }

    private enum ReceivingType
    {
        Delivery,
        SelfPickup
    }
}

