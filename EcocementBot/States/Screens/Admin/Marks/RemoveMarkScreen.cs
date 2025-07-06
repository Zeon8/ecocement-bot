using EcocementBot.Exceptions;
using EcocementBot.Helpers;
using EcocementBot.Services;
using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Marks;

public class RemoveMarkScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly MarkService _markService;

    public RemoveMarkScreen(TelegramBotClient client, Navigator navigator, MarkService markService)
    {
        _client = client;
        _navigator = navigator;
        _markService = markService;
    }

    public async Task EnterAsync(User user, Chat chat)
    {
        IEnumerable<string> marks = await _markService.GetMarks();
        var keyboard = KeyboardHelper.CreateKeyboard(marks.ToArray());
        keyboard.Add([CommonButtons.CancelButton]);

        await _client.SendMessage(chat, "Введіть марку:", replyMarkup: new ReplyKeyboardMarkup
        {
            Keyboard = keyboard
        });
    }

    public async Task HandleInput(Message message)
    {
        if (message.Text is null)
            return;

        if(message.Text == CommonButtons.CancelButton.Text)
        {
            await _navigator.GoBack(message.From!, message.Chat);
            return;
        }

        try
        {
            await _markService.RemoveMark(message.Text);
        }
        catch(MarkNotExistException)
        {
            await _client.SendMessage(message.Chat, "✖️ Марка не існує.");
            await _client.SendMessage(message.Chat, "Введіть марку:");
        }

        await _client.SendMessage(message.Chat, "Марку видалено ✅.");
        await _navigator.GoBack(message.From!, message.Chat);
    }
}
