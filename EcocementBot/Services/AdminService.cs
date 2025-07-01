using EcocementBot.States;
using EcocementBot.States.Screens.Admin;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace EcocementBot.Services;

public class AdminService
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;

    public AdminService(TelegramBotClient client, Navigator navigator)
    {
        _client = client;
        _navigator = navigator;
    }

    public async Task HandleMessage(Message message)
    {
        if(message.Text == "/start")
            await _navigator.Open<AdminScreen>(message.From, message.Chat);

        var screen = _navigator.GetScreen(message.From!);
        if (screen is not null)
            await screen.HandleInput(message);
    }
}
