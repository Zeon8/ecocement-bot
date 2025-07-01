using EcocementBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using EcocementBot.States;
using EcocementBot.States.Screens.Auth;
using EcocementBot.Data.Entities;
using EcocementBot.States.Screens.Admin;
using EcocementBot.States.Screens.Clients;

namespace EcocementBot;

public class ApplicationStartup
{
    private readonly TelegramBotClient _client;
    private readonly AdminService _adminMenu;
    private readonly Navigator _navigator;
    private readonly UserService _userService;
    private readonly SessionService _sessionService;
    private readonly ILogger<ApplicationStartup> _logger;

    public ApplicationStartup(TelegramBotClient client,
        AdminService adminMenu,
        Navigator navigator,
        UserService userService,
        SessionService sessionService,
        ILogger<ApplicationStartup> logger)
    {
        _client = client;
        _adminMenu = adminMenu;
        _navigator = navigator;
        _userService = userService;
        _sessionService = sessionService;
        _logger = logger;
    }

    public async Task Start()
    {
        await _client.GetMe();
        _client.OnMessage += (message, _) => Handle(message);
    }

    private async Task Handle(Message message)
    {
        if (message.Chat.Type != ChatType.Private)
            return;

        if (message.Text != "/start" && _navigator.TryGetScreen(message.From!, out IScreen? screen))
        {
            try
            {
                await screen.HandleInput(message);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "An exception was thrown:");
            }
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
    }
}
