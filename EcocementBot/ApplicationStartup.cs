using EcocementBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;

namespace EcocementBot;

public class ApplicationStartup
{
    private readonly TelegramBotClient _client;
    private readonly AdminService _adminMenu;

    public ApplicationStartup(TelegramBotClient client, AdminService adminMenu)
    {
        _client = client;
        _adminMenu = adminMenu;
    }

    public async Task Start()
    {
        await _client.GetMe();
        _client.OnMessage += (message, _) => _adminMenu.HandleMessage(message);
    }
}
