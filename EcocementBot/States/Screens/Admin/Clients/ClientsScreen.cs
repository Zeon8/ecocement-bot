using EcocementBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.States.Screens.Admin.Clients;

public class ClientsScreen : IScreen
{
    private readonly TelegramBotClient _client;
    private readonly Navigator _navigator;
    private readonly IServiceProvider _serviceProvider;

    public ClientsScreen(TelegramBotClient client, 
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

        var clients = await clientService.GetClients();
        var numbers = string.Join('\n', clients.Select(c => $"`+{c.PhoneNumber}` ({c.Name})"));
        await _client.SendMessage(chat, $"💼 *Клієнти*\n{numbers}\nОберіть:", 
            parseMode: ParseMode.Markdown, 
            replyMarkup: new ReplyKeyboardMarkup
        {
            Keyboard =
                [
                    [new KeyboardButton("➕ Створити")],
                    [new KeyboardButton("✍️ Редагувати")],
                    [new KeyboardButton("🗑 Видалити")],
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
            "➕ Створити" => _navigator.Open<CreateClientScreen>(message.From!, message.Chat),
            "✍️ Редагувати" => _navigator.Open<EditClientScreen>(message.From!, message.Chat),
            "🗑 Видалити" => _navigator.Open<DeleteClientScreen>(message.From!, message.Chat),
            _ => Retry(message),
        };
    }

    private async Task Retry(Message message)
    {
        await _client.SendMessage(message.Chat, "❌ Немає такого варіанту вибору.");
        await EnterAsync(message.From!, message.Chat);
    }
}
