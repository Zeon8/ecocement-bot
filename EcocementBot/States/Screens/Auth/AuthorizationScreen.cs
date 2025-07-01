using EcocementBot.Data.Entities;
using EcocementBot.Services;
using EcocementBot.States.Screens.Admin;
using EcocementBot.States.Screens.Clients;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Auth;

public class AuthorizationScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly UserService _userService;
    private readonly SessionService _sessionService;

    private string _phoneNumber;

    public AuthorizationScreen(TelegramBotClient client,
        Navigator navigator,
        UserService userService,
        SessionService sessionService)
    {
        _client = client;
        _navigator = navigator;
        _userService = userService;
        _sessionService = sessionService;
    }

    public Task EnterAsync(TelegramUser user, Chat chat)
    {
        return _client.SendMessage(chat, "Для авторизації нам потрібен ваш номер телефону.",
            replyMarkup: new KeyboardButton("☎️ Надати номер телефону")
            {
                RequestContact = true,
            });
    }

    public async Task HandleInput(Message message)
    {
        if (_phoneNumber is null)
        {
            if (message.Contact is null || message.Contact.UserId != message.From!.Id)
            {
                await _client.SendMessage(message.Chat, "✖ Хибний номер телефону. Просто натисніть кнопку знизу щоб надати номер.");
                return;
            }

            _phoneNumber = message.Contact.PhoneNumber;

            await Check(message);
        }

        if (message.Text == "🔁 Повторити вхід")
            await Check(message);

    }

    private async Task Check(Message message)
    {
        var user = await _userService.GetUser(_phoneNumber);

        if (user is null)
        {
            await _client.SendMessage(message.Chat, "Ви не авторизовані. Для реєстрації зв'яжіться з менеджером: [контакт]",
                replyMarkup: new KeyboardButton("🔁 Повторити вхід"));
            return;
        }

        await _userService.UpdateTelegramUserId(_phoneNumber, message.From!.Id);

        await _client.SendMessage(message.Chat, "✅ Авторизовано.");

        if (user.UserType == UserType.Admin)
        {
            await _navigator.Open<AdminScreen>(message.From, message.Chat);
            return;
        }

        _sessionService.Authorize(message.From.Id, user.PhoneNumber);
        await _navigator.Open<OrderScreen>(message.From, message.Chat);
    }
}
