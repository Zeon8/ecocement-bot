using EcocementBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using EcocementBot.States;
using EcocementBot.Data.Entities;
using EcocementBot.States.Screens.Admin;
using EcocementBot.States.Screens;

namespace EcocementBot;

public class StartupService : BackgroundService
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly StatePersistanceService _persistanceService;
    private readonly OrderSender _orderSender;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StartupService> _logger;

    public StartupService(TelegramBotClient client,
        Navigator navigator,
        ILogger<StartupService> logger,
        StatePersistanceService persistanceService,
        OrderSender orderSender,
        IServiceProvider serviceProvider)
    {
        _client = client;
        _navigator = navigator;
        _logger = logger;
        _persistanceService = persistanceService;
        _orderSender = orderSender;
        _serviceProvider = serviceProvider;
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

        await using var scope = _serviceProvider.CreateAsyncScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();
        var user = await userService.GetUser(message.From!.Id);

        if (message.Chat.Type == ChatType.Group)
        {
            if (message.Text != "/notify")
                return;

            if (user is null || user.Role != UserRole.Admin)
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
            if(user is null && screen is not AuthorizationScreen)
                _navigator.Clear(message.From!);
            else
            {
                await screen.HandleInput(message);
                await TrySave();
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
        await TrySave();
    }

    private async Task TrySave()
    {
        try
        {
            await _persistanceService.Save();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Saving state failed.");
        }
    }
}
