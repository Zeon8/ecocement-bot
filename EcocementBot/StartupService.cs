using EcocementBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using EcocementBot.States;
using EcocementBot.Data.Entities;
using EcocementBot.States.Screens.Admin;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using EcocementBot.States.Screens;

namespace EcocementBot;

public class StartupService : BackgroundService
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly UserService _userService;
    private readonly StatePersistanceService _persistanceService;
    private readonly OrderSender _orderSender;
    private readonly ILogger<StartupService> _logger;

    public StartupService(TelegramBotClient client,
        Navigator navigator,
        UserService userService,
        ILogger<StartupService> logger,
        StatePersistanceService persistanceService,
        OrderSender orderSender)
    {
        _client = client;
        _navigator = navigator;
        _userService = userService;
        _logger = logger;
        _persistanceService = persistanceService;
        _orderSender = orderSender;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _persistanceService.Load();

        TelegramUser user = await _client.GetMe(stoppingToken);
        _client.OnMessage += Handle;
    }

    private async Task Handle(Message message, UpdateType updateType)
    {
        if (updateType == UpdateType.EditedMessage)
            return;

        if (message.Chat.Type == ChatType.Group)
        {
            if (message.Text != "/notify")
                return;

            var user2 = await _userService.GetUser(message.From!.Id);
            if (user2 is null || user2.UserType != UserType.Admin)
                return;

            _orderSender.GroupId = message.Chat.Id;
            await _client.SendMessage(message.Chat, "Групу встановлено ✅.");
        }

        if (message.Chat.Type != ChatType.Private)
            return;

        if (message.Text == "/start")
            _navigator.Clear(message.From!);
        else if (_navigator.TryGetScreen(message.From!, out IScreen? screen))
        {
            try
            {
                await screen.HandleInput(message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "An exception was thrown.");
                return;
            }

            await _persistanceService.Save();
            return;
        }

        var user = await _userService.GetUser(message.From!.Id);
        if (user is null)
        {
            await _navigator.Open<AuthorizationScreen>(message.From, message.Chat);
            return;
        }

        if (user.UserType == UserType.Admin)
        {
            await _navigator.Open<AdminScreen>(message.From, message.Chat);
            return;
        }

        await _navigator.Open<OrderScreen>(message.From, message.Chat);
        await _persistanceService.Save();
    }
}
