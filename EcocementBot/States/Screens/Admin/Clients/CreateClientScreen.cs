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
    private readonly IServiceProvider _serviceProvider;

    private readonly Dictionary<StateType, Step> _steps;

    public CreateClientScreen(TelegramBotClient client, 
        Navigator navigator, 
        IServiceProvider serviceProvider)
    {
        _client = client;
        _navigator = navigator;
        _serviceProvider = serviceProvider;

        _steps = new()
        {
            [StateType.EnteringPhoneNumber] = new Step
            {
                Ask = (chat, _) => _client.SendMessage(chat, "*➕Створення клієнта*\n\nВведіть номер (у форматі +380XXXXXXXXX):",
                    parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: CommonButtons.CancelButton),
                Handle = async message =>
                {
                    if (message.Contact is not null)
                        State.Model.PhoneNumber = message.Contact.PhoneNumber;
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

                    await using var scoped = _serviceProvider.CreateAsyncScope();
                    var clientService = scoped.ServiceProvider.GetRequiredService<ClientService>();
                    var userService = scoped.ServiceProvider.GetRequiredService<UserService>();

                    var user = await userService.GetUser(State.Model.PhoneNumber);
                    if (user is not null)
                    {
                        if (user.Role == Data.Entities.UserRole.Admin)
                        {
                            await _client.SendMessage(message.Chat, $"❌ Це номер вже використаний адміністратором.");
                            await Ask(message);
                        }
                        else
                        {
                            var client = await clientService.GetClient(State.Model.PhoneNumber);
                            await _client.SendMessage(message.Chat, $"❌ Цей номер вже використаний клієнтом {client!.Name}.");
                            await Ask(message);
                        }
                        return;
                    }

                    State.Type = StateType.EnteringName;
                }
            },
            [StateType.EnteringName] = new Step
            {
                Ask = (chat, _) => _client.SendMessage(chat, "Введіть назву підприємства:", replyMarkup: CommonButtons.CancelButton),
                Handle = message =>
                {
                    State.Model.Name = message.Text!;
                    State.Type = StateType.EnteringAddress;
                    return Task.CompletedTask;
                }
            },
            [StateType.EnteringAddress] = new Step
            {
                Ask = (chat, _) => _client.SendMessage(chat, "Введіть адресу підприємства:", replyMarkup: CommonButtons.CancelButton),
                Handle = message =>
                {
                    State.Model.Address = message.Text!;
                    State.Type = StateType.EnteringPaymentType;
                    return Task.CompletedTask;
                }
            },
            [StateType.EnteringPaymentType] = new Step
            {
                Ask = (chat, _) => _client.SendMessage(chat, "Виберіть спосіб оплати:", replyMarkup: new ReplyKeyboardMarkup
                {
                    Keyboard =
                       [
                           [new KeyboardButton("💵 Готівка"), new KeyboardButton("🏦 Безготівка")],
                           [new KeyboardButton("💵 Готівка або 🏦 Безготівка")],
                           [CommonButtons.CancelButton],
                       ]
                }),
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

                    await using var scoped = _serviceProvider.CreateAsyncScope();
                    var clientService = scoped.ServiceProvider.GetRequiredService<ClientService>();
                    var userService = scoped.ServiceProvider.GetRequiredService<UserService>();

                    var user = await userService.GetUser(State.Model.PhoneNumber);
                    await clientService.CreateClient(State.Model);
                    await userService.CreateUser(State.Model.PhoneNumber);

                    await _client.SendMessage(message.Chat, "Клієнта додано ✅.");
                    await _navigator.GoBack(message.From!, message.Chat);
                }

            }
        };
       
    }

    public Task EnterAsync(User user, Chat chat) => Ask(chat, user);

    public async Task HandleInput(Message message)
    {
        if (message.Text is null)
            return;

        if (message.Text == CommonButtons.CancelButton.Text)
        {
            await _navigator.GoBack(message.From!, message.Chat);
            return;
        }

        StateType stateTypeBefore = State.Type;
        await _steps[stateTypeBefore].Handle(message);

        if (stateTypeBefore != State.Type)
            await Ask(message);
    }

    private Task Ask(Message message) => Ask(message.Chat, message.From!);

    private Task Ask(Chat chat, User user) => _steps[State.Type].Ask(chat, user);

    public enum StateType
    {
        EnteringPhoneNumber,
        EnteringName,
        EnteringAddress,
        EnteringPaymentType,
    }

    public class FormState
    {
        public StateType Type { get; set; } = StateType.EnteringPhoneNumber;

        public ClientModel Model { get; set; } = new();
    }
}
