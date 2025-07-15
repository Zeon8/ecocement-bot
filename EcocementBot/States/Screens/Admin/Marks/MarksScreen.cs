using EcocementBot.Services;
using EcocementBot.States.Screens.Admin.Clients;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Marks;

public class MarksScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly MarkService _markService;

    public MarksScreen(TelegramBotClient client, Navigator navigator, MarkService markService)
    {
        _client = client;
        _navigator = navigator;
        _markService = markService;
    }

    public async Task EnterAsync(User user, Chat chat)
    {
        var marks = await _markService.GetMarks();
        var markList = string.Join(", ", marks.Select(m => $"`{m}`"));

        await _client.SendMessage(chat, $"🔖 *Марки*\n\n{markList}\n\nОберіть:",
            parseMode: ParseMode.Markdown, replyMarkup: new ReplyKeyboardMarkup()
            {
                Keyboard =
                [
                    [
                        new KeyboardButton("➕ Створити"),
                        new KeyboardButton("🗑 Видалити"),
                    ],
                    [CommonButtons.BackButton],
                ]
            });
    }

    public Task HandleInput(Message message)
    {
        if (message.Text == CommonButtons.BackButton.Text)
            return _navigator.GoBack(message.From!, message.Chat);

        return message.Text switch
        {
            "➕ Створити" => _navigator.Open<CreateMarkScreen>(message.From!, message.Chat),
            "🗑 Видалити" => _navigator.Open<RemoveMarkScreen>(message.From!, message.Chat),
            _ => Retry(message.Chat, message.From!),
        };
    }

    public async Task Retry(Chat chat, User user)
    {
        await _client.SendMessage(chat, "❌ Немає такого варіанту вибору.");
        await EnterAsync(user, chat);
    }
}
