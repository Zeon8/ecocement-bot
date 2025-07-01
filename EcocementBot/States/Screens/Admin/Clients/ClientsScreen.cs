using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Clients;

public class ClientsScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;

    public ClientsScreen(TelegramBotClient client, Navigator navigator)
    {
        _client = client;
        _navigator = navigator;
    }

    public Task EnterAsync(User user, Chat chat)
    {
        return _client.SendMessage(chat, "💼 *Клієнти*\n\nОберіть:", 
            parseMode: ParseMode.Markdown, 
            replyMarkup: new ReplyKeyboardMarkup
        {
            Keyboard =
                [
                    [
                        new KeyboardButton("➕ Створити"),
                        new KeyboardButton("✍️ Редагувати"),
                        new KeyboardButton("🗑 Видалити"),
                    ],
                    [CommonButtons.BackButton],
                ]
        });
    }

    public Task HandleInput(Message message)
    {
        if (message.Text == CommonButtons.BackButton.Text)
            _navigator.GoBack(message.From!, message.Chat);

        if (message.Text == "➕ Створити")
            return _navigator.Open<CreateClientScreen>(message.From!, message.Chat);
        if (message.Text == "✍️ Редагувати")
            return _navigator.Open<EditClientScreen>(message.From!, message.Chat);
        if(message.Text == "🗑 Видалити")
            return _navigator.Open<DeleteClientScreen>(message.From!, message.Chat);

        return Task.CompletedTask;
    }
}
