using EcocementBot.Exceptions;
using EcocementBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Clients;

public class DeleteClientScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly IServiceProvider _serviceProvider;

    public DeleteClientScreen(TelegramBotClient client,
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
        var clientService = scoped.ServiceProvider.GetRequiredService<ClientService>();

        IEnumerable<string> phoneNumbers = await clientService.GetPhoneNumbers();
        await _client.SendMessage(chat, "*🗑 Видалення клієнта*\n\nВведіть номер клієнта:",
            parseMode: ParseMode.Markdown,
            replyMarkup: new ReplyKeyboardMarkup
            {
                Keyboard =
                [
                    ..phoneNumbers.Select(p => new[]{ new KeyboardButton('+'+p) }).ToArray(),
                    [CommonButtons.CancelButton],
                ]
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
        var clientService = scoped.ServiceProvider.GetRequiredService<ClientService>();
        var userService = scoped.ServiceProvider.GetRequiredService<UserService>();

        string phoneNumber = CommonRegex.NonDigitSymbols.Replace(message.Text, string.Empty);
        try
        {
            await clientService.DeleteClient(phoneNumber);
        }
        catch(ClientNotFoundException)
        {
            await _client.SendMessage(message.Chat, "❌ Клієнта з таким номером не знайдено.");
            await EnterAsync(message.From!, message.Chat);
            return;
        }

        await userService.DeleteUser(phoneNumber);

        await _client.SendMessage(message.Chat, "Клієнта видалено ✅.");
        await _navigator.GoBack(message.From!, message.Chat);
    }
}
