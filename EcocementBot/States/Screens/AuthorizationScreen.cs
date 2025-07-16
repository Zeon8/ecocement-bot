using EcocementBot.Data.Entities;
using EcocementBot.Services;
using EcocementBot.States.Screens.Admin;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens;

public partial class AuthorizationScreen : IScreen
{
    public string? PhoneNumber { get; set; }

    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    private static readonly KeyboardButton s_phoneNumberButton = new("☎️ Надати номер телефону")
    {
        RequestContact = true,
    };

    public AuthorizationScreen(TelegramBotClient client,
        Navigator navigator,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _client = client;
        _navigator = navigator;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    public Task EnterAsync(TelegramUser user, Chat chat)
    {
        return _client.SendMessage(chat, "Для авторизації нам потрібен ваш номер телефону.",
            replyMarkup: s_phoneNumberButton);
    }

    public async Task HandleInput(Message message)
    {
        if (PhoneNumber is null)
        {
            if (message.Contact is null || message.Contact.UserId != message.From!.Id)
            {
                await _client.SendMessage(message.Chat, "✖ Хибний номер телефону. Просто натисніть кнопку знизу щоб надати номер.", 
                    replyMarkup: s_phoneNumberButton);
                return;
            }

            PhoneNumber = CommonRegex.NonDigitSymbols.Replace(message.Contact.PhoneNumber, string.Empty);

            await Check(message.Chat, message.From!);
            return;
        }

        if (message.Text == "🔁 Повторити вхід")
            await Check(message.Chat, message.From!);
    }

    private async Task Check(Chat chat, TelegramUser telegramUser)
    {
        await using var scoped = _serviceProvider.CreateAsyncScope();
        var userService = scoped.ServiceProvider.GetRequiredService<UserService>();

        var user = await userService.GetUser(PhoneNumber!);
        if (user is null)
        {
            var contact = _configuration["ManagerContact"]
                ?? throw new InvalidOperationException("ManagerContact not found in configuration."); 

            await _client.SendMessage(chat, $"Ви не авторизовані. Для реєстрації зв'яжіться з менеджером: {contact}",
                replyMarkup: new KeyboardButton("🔁 Повторити вхід"));
            return;
        }

        await userService.UpdateTelegramUserId(PhoneNumber!, telegramUser.Id);

        await _client.SendMessage(chat, "✅ Авторизовано.");
        _navigator.Clear(telegramUser);

        if (user.Role == UserRole.Admin)
        {
            await _navigator.Open<AdminScreen>(telegramUser, chat);
            return;
        }

        await _navigator.Open<OrderScreen>(telegramUser, chat);
    }


}
