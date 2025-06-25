using System.Collections.Concurrent;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using Telegram.Bot;

public class AsyncTelegramClient : TelegramBotClient
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Message>> _pendingMessages = new();
    public event OnMessageHandler OnAsyncMessage;

    public AsyncTelegramClient(string token, HttpClient? httpClient = null, CancellationToken cancellationToken = default) 
        : base(token, httpClient, cancellationToken)
    {

    }

    public void Start(CancellationToken cancellationToken = default)
    {
        this.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            new ReceiverOptions { AllowedUpdates = { } },
            cancellationToken: cancellationToken
        );
    }

    private Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Type == UpdateType.Message && update.Message!.Text != null)
        {
            OnAsyncMessage?.Invoke(update.Message, update.Type);

            long userId = update.Message.From!.Id;

            // Complete any waiting task
            if (_pendingMessages.TryRemove(userId, out var tcs))
                tcs.SetResult(update.Message);
        }
        return Task.CompletedTask;
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken ct)
    {
        Console.WriteLine($"Error: {exception.Message}");
        return Task.CompletedTask;
    }

    public async Task<Message> SendMessageAndWaitForReplyAsync(Chat chat, string text, ReplyMarkup? replyMarkup = null)
    {
        await this.SendMessage(chat, text, replyMarkup: replyMarkup);
        return await WaitForMessageAsync(chat);
    }

    public async Task<Message> WaitForMessageAsync(Chat chat)
    {
        var tcs = new TaskCompletionSource<Message>();
        _pendingMessages[chat.Id] = tcs;
        return await tcs.Task;
    }
}