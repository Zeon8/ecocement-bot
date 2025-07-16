using EcocementBot.Exceptions;
using EcocementBot.Helpers;
using EcocementBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Marks;

public class RemoveMarkScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly IServiceProvider _serviceProvider;

    public RemoveMarkScreen(TelegramBotClient client, 
        Navigator navigator, 
        IServiceProvider serviceProvider)
    {
        _client = client;
        _navigator = navigator;
        _serviceProvider = serviceProvider;
    }

    public async Task EnterAsync(User user, Chat chat)
    {
        await using var scoped = _serviceProvider.CreateAsyncScope();
        var markService = scoped.ServiceProvider.GetRequiredService<MarkService>();

        IEnumerable<string> marks = await markService.GetMarks();
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

        await using var scoped = _serviceProvider.CreateAsyncScope();
        var markService = scoped.ServiceProvider.GetRequiredService<MarkService>();

        try
        {
            await markService.RemoveMark(message.Text);
        }
        catch(MarkNotExistException)
        {
            await _client.SendMessage(message.Chat, "❌ Марка не існує.");
            await EnterAsync(message.From!, message.Chat);
            return;
        }

        await _client.SendMessage(message.Chat, "Марку видалено ✅.");
        await _navigator.GoBack(message.From!, message.Chat);
    }
}
