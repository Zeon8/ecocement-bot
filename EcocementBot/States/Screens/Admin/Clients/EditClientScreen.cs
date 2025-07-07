using EcocementBot.Data.Entities;
using EcocementBot.Data.Enums;
using EcocementBot.Models;
using EcocementBot.Services;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Clients;

public class EditClientScreen : IScreen
{
    public FormState State { get; set; } = new();

    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly ClientService _clientService;
    private readonly UserService _userService;

    public EditClientScreen(TelegramBotClient client,
        Navigator navigator,
        ClientService clientService,
        UserService userService)
    {
        _client = client;
        _navigator = navigator;
        _clientService = clientService;
        _userService = userService;
    }

    public async Task EnterAsync(TelegramUser user, Chat chat)
    {
        IEnumerable<string> phoneNumbers = await _clientService.GetPhoneNumbers();
        await _client.SendMessage(chat, "*✍️ Редагування клієнта*\n\nВведіть номер клієнта:",
            parseMode: ParseMode.Markdown,
            replyMarkup: new ReplyKeyboardMarkup
            {
                Keyboard = 
                [
                    ..phoneNumbers.Select(p => new[]{ new KeyboardButton('+'+p) }).ToArray(),
                    [CommonButtons.CancelButton],
                ]
            });
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
            case StateTypes.FindClient:
                string? phoneNumber = message.Text[1..]; // Skip +
                var client = await _clientService.GetClient(phoneNumber);
                if (client is null)
                {
                    await _client.SendMessage(message.Chat, "✖️ Клієнта за цим номером не знайдено.\nВведіть номер:");
                    return;
                }

                await _client.SendMessage(message.Chat,
                    text: "Введіть новий номер:",
                    replyMarkup: CreateFieldKeyboard(client.PhoneNumber));

                State.Model = new()
                {
                    Name = client.Name,
                    PhoneNumber = phoneNumber,
                    Address = client.Address,
                    PaymentType = client.PaymentType,
                };
                State.OldPhoneNumber = phoneNumber;

                State.Type = StateTypes.EnteringPhoneNumber;
                break;
            case StateTypes.EnteringPhoneNumber:
                if (message.Contact is Contact contact)
                    State.Model.PhoneNumber = contact.PhoneNumber;
                else
                {
                    if (!CommonRegex.PhoneNumber.IsMatch(message.Text))
                    {
                        await _client.SendMessage(message.Chat, "✖️ Неправильний формат.");
                        break;
                    }

                    State.Model.PhoneNumber = message.Text;
                }

                var existingClient = await _clientService.GetClient(State.Model.PhoneNumber);
                if (existingClient is not null)
                {
                    await _client.SendMessage(message.Chat, $"✖️ Цей номер вже використаний клієнтом {existingClient.Name}.");
                    break;
                }

                await _client.SendMessage(message.Chat,
                    text: "Введіть назву підприємства:",
                    replyMarkup: CreateFieldKeyboard(State.Model.Name));

                State.Type = StateTypes.EnteringName;
                break;
            case StateTypes.EnteringName:
                State.Model.Name = message.Text;

                await _client.SendMessage(message.Chat,
                    text: "Введіть адресу підприємства:",
                    replyMarkup: CreateFieldKeyboard(State.Model.Address));

                State.Type = StateTypes.EnteringAddress;
                break;
            case StateTypes.EnteringAddress:
                State.Model.Address = message.Text;

                string oldType = State.Model.PaymentType switch
                {
                    PaymentType.Cash => "💵 Готівка",
                    PaymentType.Card => "💳 Карта",
                };

                await _client.SendMessage(message.Chat, $"Виберіть спосіб оплати ({oldType}):",
                    replyMarkup: new ReplyKeyboardMarkup
                    {
                        Keyboard =
                           [
                               [new KeyboardButton("💵 Готівка"), new KeyboardButton("💳 Карта")],
                               [CommonButtons.CancelButton]
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
                await _clientService.UpdateClient(State.Model);

                if(State.OldPhoneNumber != State.Model.PhoneNumber)
                    await _userService.UpdateUserPhone(State.OldPhoneNumber!, State.Model.PhoneNumber);

                await _client.SendMessage(message.Chat, "Дані клієнта оновлено ✅.");
                await _navigator.GoBack(message.From!, message.Chat);
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
                [CommonButtons.CancelButton],
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

        public string? OldPhoneNumber { get; set; }
    }
}
