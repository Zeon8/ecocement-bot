using EcocementBot.Exceptions;
using EcocementBot.Services;
using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Mark;

public class CreateMarkScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly MarkService _markService;

    private static readonly KeyboardButton _cancelButton = new KeyboardButton("🚫 Скасувати");
    private static readonly ReplyKeyboardMarkup _cancelKeyboard = new()
    {
        Keyboard = [[_cancelButton]]
    };

    public CreateMarkScreen(TelegramBotClient client, Navigator navigator, MarkService markService)
    {
        _client = client;
        _navigator = navigator;
        _markService = markService;
    }

    public Task EnterAsync(User user, Chat chat)
    {
        return _client.SendMessage(chat, "Введіть марку:", replyMarkup: _cancelKeyboard );
    }

    public async Task HandleInput(Message message)
    {
        if (message.Text == _cancelButton.Text)
        {
            await _navigator.PopScreen(message.From!, message.Chat);
            return;
        }

        try
        {
            await _markService.CreateMark(message.Text!);
        }
        catch(MarkExistsException)
        {
            await _client.SendMessage(message.Chat, "✖️ Марка вже існує.");
            await _client.SendMessage(message.Chat, "Введіть марку:", replyMarkup: _cancelKeyboard);
            return;
        }

        await _client.SendMessage(message.Chat, "Марку створено ✅", replyMarkup: _cancelKeyboard);
        await _navigator.PopScreen(message.From!, message.Chat);
    }
}
