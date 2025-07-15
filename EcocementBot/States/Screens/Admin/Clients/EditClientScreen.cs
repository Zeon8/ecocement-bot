using EcocementBot.Data.Entities;
using EcocementBot.Data.Enums;
using EcocementBot.Models;
using EcocementBot.Services;
using System;
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

    private readonly Dictionary<StateTypes, Step> _steps;

    public EditClientScreen(TelegramBotClient client,
        Navigator navigator,
        ClientService clientService,
        UserService userService)
    {
        _client = client;
        _navigator = navigator;
        _clientService = clientService;
        _userService = userService;

        _steps = new()
        {
            [StateTypes.FindClient] = new Step
            {
                Ask = async (chat, user) =>
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
                },
                Handle = async message =>
                {
                    string? phoneNumber = CommonRegex.NonDigitSymbols.Replace(message.Text!, string.Empty);
                    var client = await _clientService.GetClient(phoneNumber);
                    if (client is null)
                    {
                        await _client.SendMessage(message.Chat, "❌ Клієнта за цим номером не знайдено.\nВведіть номер:");
                        await Ask(message);
                        return;
                    }

                    State.Model = new()
                    {
                        Name = client.Name,
                        PhoneNumber = phoneNumber,
                        Address = client.Address,
                        PaymentType = client.PaymentType,
                    };
                    State.OldPhoneNumber = phoneNumber;

                    State.Type = StateTypes.EnteringPhoneNumber;
                }
            },
            [StateTypes.EnteringPhoneNumber] = new Step
            {
                Ask = (chat, _) => _client.SendMessage(chat, "Введіть новий номер:", 
                    replyMarkup: CreateFieldKeyboard('+' + State.Model.PhoneNumber)),
                Handle = async message =>
                {
                    if (message.Contact is Contact contact)
                        State.Model.PhoneNumber = CommonRegex.NonDigitSymbols.Replace(contact.PhoneNumber, string.Empty);
                    else
                    {
                        if (!CommonRegex.PhoneNumber.IsMatch(message.Text!))
                        {
                            await _client.SendMessage(message.Chat, "❌ Неправильний формат.");
                            await Ask(message);
                            return;
                        }

                        State.Model.PhoneNumber = CommonRegex.NonDigitSymbols.Replace(message.Text!, string.Empty);
                    }

                    if (State.OldPhoneNumber != State.Model.PhoneNumber)
                    {
                        var user = await _userService.GetUser(State.Model.PhoneNumber);
                        if (user is not null)
                        {
                            if (user.Role == UserRole.Admin)
                            {
                                await _client.SendMessage(message.Chat, $"❌ Це номер вже використаний адміністратором.");
                                await Ask(message);
                            }
                            else
                            {
                                var client = await _clientService.GetClient(State.Model.PhoneNumber);
                                await _client.SendMessage(message.Chat, $"❌ Цей номер вже використаний клієнтом {client!.Name}.");
                                await Ask(message);
                            }
                            return;
                        }
                    }

                    State.Type = StateTypes.EnteringName;

                }
            },
            [StateTypes.EnteringName] = new Step
            { 
                Ask = (chat, _) => _client.SendMessage(chat, "Введіть назву підприємства:",
                    replyMarkup: CreateFieldKeyboard(State.Model.Name)),
                Handle = message =>
                {
                    State.Model.Name = message.Text!;
                    State.Type = StateTypes.EnteringAddress;
                    return Task.CompletedTask;
                }
            },
            [StateTypes.EnteringAddress] = new Step
            {
                Ask = (chat, _) => _client.SendMessage(chat, "Введіть адресу підприємства:",
                    replyMarkup: CreateFieldKeyboard(State.Model.Address)),
                Handle = message =>
                {
                    State.Model.Address = message.Text!;
                    State.Type = StateTypes.EnteringPaymentType;
                    return Task.CompletedTask;
                }
            },
            [StateTypes.EnteringPaymentType] = new Step
            {
                Ask = (chat, _) =>
                {
                    string oldType = State.Model.PaymentType switch
                    {
                        ClientPaymentType.Cash => "💵 Готівка",
                        ClientPaymentType.Cashless => "🏦 Безготівка",
                        ClientPaymentType.Both => "🏦 Безготівка або 💵 Готівка"
                    };

                    return _client.SendMessage(chat, $"Виберіть спосіб оплати ({oldType}):",
                        replyMarkup: new ReplyKeyboardMarkup
                        {
                            Keyboard =
                               [
                                   [new KeyboardButton("💵 Готівка"), new KeyboardButton("🏦 Безготівка")],
                                   [new KeyboardButton("💵 Готівка або 🏦 Безготівка")],
                                   [CommonButtons.CancelButton]
                               ]
                        });
                },
                Handle = async message =>
                {
                    if (message.Text == "💵 Готівка")
                        State.Model.PaymentType = ClientPaymentType.Cash;
                    else if (message.Text == "🏦 Безготівка")
                        State.Model.PaymentType = ClientPaymentType.Cashless;
                    else if (message.Text == "💵 Готівка або 🏦 Безготівка")
                        State.Model.PaymentType = ClientPaymentType.Both;
                    else
                    {
                        await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
                        await Ask(message);
                        return;
                    }
                    await _clientService.UpdateClient(State.Model);

                    if (State.OldPhoneNumber != State.Model.PhoneNumber)
                        await _userService.UpdateUserPhone(State.OldPhoneNumber!, State.Model.PhoneNumber);

                    await _client.SendMessage(message.Chat, "Дані клієнта оновлено ✅.");
                    await _navigator.GoBack(message.From!, message.Chat);
                }
            }
        };
    }

    public Task EnterAsync(TelegramUser user, Chat chat) => Ask(chat, user);

    public async Task HandleInput(Message message)
    {
        if (message.Text is null)
            return;

        if (message.Text == CommonButtons.CancelButton.Text)
        {
            await _navigator.GoBack(message.From!, message.Chat);
            return;
        }

        var stateTypeBefore = State.Type;
        await _steps[stateTypeBefore].Handle(message);

        if (stateTypeBefore != State.Type)
            await Ask(message);
    }

    public Task Ask(Message message) => Ask(message.Chat, message.From!);

    private Task Ask(Chat chat, TelegramUser user) => _steps[State.Type].Ask(chat, user);

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
