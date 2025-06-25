using EcocementBot.States.Screens.Admin.Clients;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin;

public class AdminScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;

    public AdminScreen(TelegramBotClient client, Navigator navigator)
    {
        _client = client;
        _navigator = navigator;
    }

    public Task EnterAsync(User user, Chat chat)
    {
        return _client.SendMessage(chat, "*🏛 Головне меню*\nОберіть:", parseMode: ParseMode.Markdown, 
            replyMarkup: new ReplyKeyboardMarkup
        {
            Keyboard =
               [
                   [
                        new KeyboardButton("👤 Клієнти"),
                        new KeyboardButton("™️ Марки"),
                    ]
               ]
        });
    }

    public Task HandleInput(Message message)
    {
        if (message.Text == "👤 Клієнти")
            return _navigator.PushScreen<ClientsScreen>(message.From!, message.Chat);

        return Task.CompletedTask;
    }
}
