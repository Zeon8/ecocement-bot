using EcocementBot.Exceptions;
using EcocementBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Marks;

public class CreateMarkScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly IServiceProvider _serviceProvider;

    public CreateMarkScreen(TelegramBotClient client, 
        Navigator navigator, 
        IServiceProvider serviceProvider)
    {
        _client = client;
        _navigator = navigator;
        _serviceProvider = serviceProvider;
    }

    public Task EnterAsync(User user, Chat chat)
    {
        return _client.SendMessage(chat, "Введіть марку:", replyMarkup: CommonButtons.CancelButton );
    }

    public async Task HandleInput(Message message)
    {
        if (message.Text is null)
            return;

        if (message.Text == CommonButtons.CancelButton.Text)
        {
            await _navigator.GoBack(message.From!, message.Chat);
            return;
        }

        await using var scoped = _serviceProvider.CreateAsyncScope();
        var markService = scoped.ServiceProvider.GetRequiredService<MarkService>();

        try
        {
            await markService.CreateMark(message.Text!);
        }
        catch(MarkExistsException)
        {
            await _client.SendMessage(message.Chat, "❌ Марка вже існує.");
            await EnterAsync(message.From!, message.Chat);
            return;
        }

        await _client.SendMessage(message.Chat, "Марку створено ✅.");
        await _navigator.GoBack(message.From!, message.Chat);
    }
}
