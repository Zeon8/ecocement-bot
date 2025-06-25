using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Clients;

public class ClientsScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;

    private readonly KeyboardButton _backButton = new("⬅️ Назад");

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
                    [_backButton],
                ]
        });
    }

    public Task HandleInput(Message message)
    {
        if (message.Text == _backButton.Text)
            _navigator.PopScreen(message.From, message.Chat);

        if (message.Text == "➕ Створити")
            return _navigator.PushScreen<CreateClientScreen>(message.From!, message.Chat);
        if (message.Text == "✍️ Редагувати")
            return _navigator.PushScreen<EditClientScreen>(message.From!, message.Chat);
        if(message.Text == "🗑 Видалити")
            return _navigator.PushScreen<DeleteClientScreen>(message.From!, message.Chat);

        return Task.CompletedTask;
    }
}
