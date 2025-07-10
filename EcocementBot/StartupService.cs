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
using System;

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
        _client.OnMessage += OnMessage;
        _client.OnError += (exception, source) =>
        {
            _logger.LogError(exception, "An exception was thrown.");
            return Task.CompletedTask;
        };
    }

    private async Task OnMessage(Message message, UpdateType updateType)
    {
        if (updateType == UpdateType.EditedMessage)
            return;

        if (message.Chat.Type == ChatType.Group)
        {
            if (message.Text != "/notify")
                return;

            var user2 = await _userService.GetUser(message.From!.Id);
            if (user2 is null || user2.Role != UserRole.Admin)
                return;

            _orderSender.GroupId = message.Chat.Id;
            await _client.SendMessage(message.Chat, "Групу встановлено ✅.");
        }

        if (message.Chat.Type != ChatType.Private)
            return;

        var user = await _userService.GetUser(message.From!.Id);
        
        if (message.Text == "/start")
            _navigator.Clear(message.From!);
        else if (_navigator.TryGetScreen(message.From!, out IScreen? screen))
        {
            if(user is null && screen is not AuthorizationScreen)
                _navigator.Clear(message.From!);
            else
            {
                await screen.HandleInput(message);
                await _persistanceService.Save();
                return;
            }
        }

        if (user is null)
        {
            await _navigator.Open<AuthorizationScreen>(message.From, message.Chat);
            return;
        }
        if (user.Role == UserRole.Admin)
        {
            await _navigator.Open<AdminScreen>(message.From, message.Chat);
            return;
        }

        await _navigator.Open<OrderScreen>(message.From, message.Chat);
        await _persistanceService.Save();
    }
}
