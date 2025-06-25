using EcocementBot.Data.Enums;
using EcocementBot.Models;
using EcocementBot.Services;
using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Clients;

public class CreateClientScreen : IScreen
{
    public FormState State { get; } = new();

    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly ClientService _clientService;

    private static readonly KeyboardButton _cancelButton = new KeyboardButton("🚫 Скасувати");

    private static readonly ReplyKeyboardMarkup _cancelKeyboard = new()
    {
        Keyboard = [[_cancelButton]]
    };

    public CreateClientScreen(TelegramBotClient client, Navigator navigator, ClientService clientService)
    {
        _client = client;
        _navigator = navigator;
        _clientService = clientService;
    }

    public Task EnterAsync(User user, Chat chat)
    {
        return _client.SendMessage(chat, "*➕Створення клієнта*\n\nВведіть номер:", 
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, 
            replyMarkup: _cancelKeyboard);
    }

    public async Task HandleInput(Message message)
    {
        if (message.Text == _cancelButton.Text)
        {
            await _navigator.PopScreen(message.From, message.Chat);
            return;
        }

        switch (State.Type)
        {
            case StateTypes.EnteringPhoneNumber:
                State.Model.PhoneNumber = message.Text;
                await _client.SendMessage(message.Chat, "Введіть назву підприємства:", replyMarkup: _cancelKeyboard);
                State.Type = StateTypes.EnteringName;
                break;
            case StateTypes.EnteringName:
                State.Model.Name = message.Text;
                await _client.SendMessage(message.Chat, "Введіть адресу підприємства:");
                State.Type = StateTypes.EnteringAddress;
                break;
            case StateTypes.EnteringAddress:
                State.Model.Address = message.Text;
                await _client.SendMessage(message.Chat, "Виберіть спосіб доставки:", replyMarkup: new ReplyKeyboardMarkup
                {
                    Keyboard =
                       [
                           [new KeyboardButton("💵 Готівка"), new KeyboardButton("💳 Карта")],
                           [_cancelButton],
                       ]
                });
                State.Type = StateTypes.EnteringPaymentType;
                break;
            case StateTypes.EnteringPaymentType:
                if (message.Text == "💵 Готівка")
                    State.Model.PaymentType = PaymentType.Cash;
                else if (message.Text == "💳 Карта")
                    State.Model.PaymentType = PaymentType.Card;
                else
                {
                    await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                    break;
                }
                await _clientService.CreateClient(State.Model);
                await _client.SendMessage(message.Chat, "Клієнта додано ✅.");
                await _navigator.PopScreen(message.From, message.Chat);
                return;
        }
    }

    public enum StateTypes
    {
        EnteringPhoneNumber,
        EnteringName,
        EnteringAddress,
        EnteringPaymentType,
    }

    public class FormState
    {
        public StateTypes Type { get; set; } = StateTypes.EnteringPhoneNumber;

        public ClientModel Model { get; set; } = new();
    }
}
