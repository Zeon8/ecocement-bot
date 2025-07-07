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
    private readonly SessionService _sessionService;
    private readonly StatePersistanceService _persistanceService;
    private readonly ILogger<StartupService> _logger;

    public StartupService(TelegramBotClient client,
        Navigator navigator,
        UserService userService,
        SessionService sessionService,
        ILogger<StartupService> logger,
        StatePersistanceService persistanceService)
    {
        _client = client;
        _navigator = navigator;
        _userService = userService;
        _sessionService = sessionService;
        _logger = logger;
        _persistanceService = persistanceService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _persistanceService.Load();

        TelegramUser user = await _client.GetMe(stoppingToken);
        _client.OnMessage += (message, _) => Handle(message);
    }

    private async Task Handle(Message message)
    {
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
                _logger.LogError(exception, "An exception was thrown:");
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

        _sessionService.Authorize(message.From.Id, user.PhoneNumber);
        await _navigator.Open<OrderScreen>(message.From, message.Chat);
        await _persistanceService.Save();
    }
}
