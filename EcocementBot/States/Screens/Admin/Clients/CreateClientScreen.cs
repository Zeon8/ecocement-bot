using EcocementBot.Data.Enums;
using EcocementBot.Models;
using EcocementBot.Services;
using System;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Clients;

public partial class CreateClientScreen : IScreen
{
    public FormState State { get; set; } = new();

    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly ClientService _clientService;
    private readonly UserService _userService;

    public CreateClientScreen(TelegramBotClient client, Navigator navigator, ClientService clientService, UserService userService)
    {
        _client = client;
        _navigator = navigator;
        _clientService = clientService;
        _userService = userService;
    }

    public Task EnterAsync(User user, Chat chat)
    {
        return _client.SendMessage(chat, "*➕Створення клієнта*\n\nВведіть номер (у форматі +380XXXXXXXXX):",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: CommonButtons.CancelButton);
    }

    public async Task HandleInput(Message message)
    {
        if (message.Text is null)
            return;

        if (message.Text == CommonButtons.CancelButton.Text)
        {
            await _navigator.GoBack(message.From!, message.Chat);
            return;
        }

        switch (State.Type)
        {
            case StateTypes.EnteringPhoneNumber:
                if (message.Contact is not null)
                    State.Model.PhoneNumber = message.Contact.PhoneNumber;
                else
                {
                    if (!CommonRegex.PhoneNumber.IsMatch(message.Text))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Неправильний формат.");
                        break;
                    }

                    State.Model.PhoneNumber = message.Text[1..]; // Skip + before 380
                }

                var client = await _clientService.GetClient(State.Model.PhoneNumber);
                if (client is not null)
                {
                    await _client.SendMessage(message.Chat, $"✖️ Цей номер вже використаний клієнтом {client.Name}.");
                    break;
                }

                await _client.SendMessage(message.Chat, "Введіть назву підприємства:");
                State.Type = StateTypes.EnteringName;
                break;
            case StateTypes.EnteringName:
                State.Model.Name = message.Text;
                await _client.SendMessage(message.Chat, "Введіть адресу підприємства:");
                State.Type = StateTypes.EnteringAddress;
                break;
            case StateTypes.EnteringAddress:
                State.Model.Address = message.Text;
                await _client.SendMessage(message.Chat, "Виберіть спосіб оплати:", replyMarkup: new ReplyKeyboardMarkup
                {
                    Keyboard =
                       [
                           [new KeyboardButton("💵 Готівка"), new KeyboardButton("💳 Карта")],
                           [new KeyboardButton("💳 Карта або 💵 Готівка")],
                           [CommonButtons.CancelButton],
                       ]
                });
                State.Type = StateTypes.EnteringPaymentType;
                break;
            case StateTypes.EnteringPaymentType:
                if (message.Text == "💵 Готівка")
                    State.Model.PaymentType = ClientPaymentType.Cash;
                else if (message.Text == "💳 Карта")
                    State.Model.PaymentType = ClientPaymentType.Card;
                else if (message.Text == "💳 Карта або 💵 Готівка")
                    State.Model.PaymentType = ClientPaymentType.Both;
                else
                {
                    await _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору.");
                    break;
                }
                await _clientService.CreateClient(State.Model);
                await _userService.CreateUser(State.Model.PhoneNumber);
                await _client.SendMessage(message.Chat, "Клієнта додано ✅.");
                await _navigator.GoBack(message.From!, message.Chat);
                break;
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
