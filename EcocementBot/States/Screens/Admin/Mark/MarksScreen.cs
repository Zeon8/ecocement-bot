using EcocementBot.Services;
using EcocementBot.States.Screens.Admin.Clients;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Mark;

public class MarksScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly MarkService _markService;

    private readonly KeyboardButton _backButton = new("⬅️ Назад");

    public MarksScreen(TelegramBotClient client, Navigator navigator, MarkService markService)
    {
        _client = client;
        _navigator = navigator;
        _markService = markService;
    }

    public async Task EnterAsync(User user, Chat chat)
    {
        var marks = await _markService.GetMarks();
        var markList = string.Join(',', marks.Select(m => $"`{m}`"));

        await _client.SendMessage(chat, $"🔖 *Марки*\n\n{markList}\n\nОберіть:",
            parseMode: ParseMode.Markdown,
            replyMarkup: new ReplyKeyboardMarkup
            {
                Keyboard =
                [
                    [
                        new KeyboardButton("➕ Створити"),
                        new KeyboardButton("🗑 Видалити"),
                    ],
                    [_backButton],
                ]
            });
    }

    public Task HandleInput(Message message)
    {
        if (message.Text == _backButton.Text)
            _navigator.PopScreen(message.From!, message.Chat);

        if (message.Text == "➕ Створити")
            _navigator.PushScreen<CreateMarkScreen>(message.From!, message.Chat);

        return Task.CompletedTask;
    }
}
