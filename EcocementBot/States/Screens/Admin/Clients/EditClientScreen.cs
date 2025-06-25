using EcocementBot.Data.Entities;
using EcocementBot.Data.Enums;
using EcocementBot.Models;
using EcocementBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Clients;

public class EditClientScreen : IScreen
{
    private readonly FormState _state = new();
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly ClientService _clientService;

    private static readonly KeyboardButton _cancelButton = new KeyboardButton("🚫 Скасувати");

    private static readonly ReplyKeyboardMarkup _goBackKeyboard = new()
    {
        Keyboard = [[_cancelButton]]
    };

    public EditClientScreen(TelegramBotClient client, Navigator navigator, ClientService clientService)
    {
        _client = client;
        _navigator = navigator;
        _clientService = clientService;
    }

    public Task EnterAsync(User user, Chat chat)
    {
        return _client.SendMessage(chat, "*✍️ Редагування клієнта*\n\nВведіть номер клієнта:",
            parseMode: ParseMode.Markdown,
            replyMarkup: _goBackKeyboard);
    }

    public async Task HandleInput(Message message)
    {
        if (message.Text == _cancelButton.Text)
        {
            await _navigator.PopScreen(message.From!, message.Chat);
            return;
        }

        switch (_state.Type)
        {
            case StateTypes.FindClient:
                string? phoneNumber = message.Text;
                var client = await _clientService.GetClient(phoneNumber);
                if (client is null)
                {
                    await _client.SendMessage(message.Chat, "✖️ Клієнта за цим номером не знайдено.\nВведіть номер:");
                    return;
                }

                await _client.SendMessage(message.Chat,
                    text: "Введіть новий номер:",
                    replyMarkup: CreateFieldKeyboard(client.PhoneNumber));

                _state.Model = new()
                {
                    Name = client.Name,
                    PhoneNumber = phoneNumber,
                    Address = client.Address,
                    PaymentType = client.PaymentType,
                };

                _state.Type = StateTypes.EnteringPhoneNumber;
                break;
            case StateTypes.EnteringPhoneNumber:
                _state.Model.PhoneNumber = message.Text;

                await _client.SendMessage(message.Chat,
                    text: "Введіть назву підприємства:",
                    replyMarkup: CreateFieldKeyboard(_state.Model.Name));

                _state.Type = StateTypes.EnteringName;
                break;
            case StateTypes.EnteringName:
                _state.Model.Name = message.Text;

                await _client.SendMessage(message.Chat,
                    text: "Введіть адресу підприємства:",
                    replyMarkup: CreateFieldKeyboard(_state.Model.Address));

                _state.Type = StateTypes.EnteringAddress;
                break;
            case StateTypes.EnteringAddress:
                _state.Model.Address = message.Text;

                string oldType = _state.Model.PaymentType switch
                {
                    PaymentType.Cash => "💵 Готівка",
                    PaymentType.Card => "💳 Карта",
                };

                await _client.SendMessage(message.Chat, $"Виберіть спосіб доставки ({oldType}):",
                    replyMarkup: new ReplyKeyboardMarkup
                    {
                        Keyboard =
                           [
                               [new KeyboardButton("💵 Готівка"), new KeyboardButton("💳 Карта")],
                               [new KeyboardButton("🚫 Скасувати")]
                           ]
                    });

                _state.Type = StateTypes.EnteringPaymentType;
                break;
            case StateTypes.EnteringPaymentType:
                if (message.Text == "💵 Готівка")
                    _state.Model.PaymentType = PaymentType.Cash;
                else if (message.Text == "💳 Карта")
                    _state.Model.PaymentType = PaymentType.Card;
                else
                {
                    await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                    break;
                }
                await _clientService.UpdateClient(_state.Model);
                await _client.SendMessage(message.Chat, "Дані клієнта оновлено ✅.");
                await _navigator.PopScreen(message.From, message.Chat);
                return;
        }


    }

    private ReplyKeyboardMarkup? CreateFieldKeyboard(string oldValue)
    {
        return new ReplyKeyboardMarkup
        {
            Keyboard =
            [
                [new KeyboardButton(oldValue)],
                [_cancelButton],
            ]
        };
    }

    public enum StateTypes
    {
        FindClient,
        EnteringPhoneNumber,
        EnteringName,
        EnteringAddress,
        EnteringPaymentType,
    }

    public class FormState
    {
        public StateTypes Type { get; set; } = StateTypes.FindClient;

        public ClientModel Model { get; set; } = new();
    }

}
