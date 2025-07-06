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
    private readonly ClientService _clientService;

    public ClientsScreen(TelegramBotClient client, Navigator navigator, ClientService clientService)
    {
        _client = client;
        _navigator = navigator;
        _clientService = clientService;
    }

    public async Task EnterAsync(User user, Chat chat)
    {
        var clients = await _clientService.GetClients();
        var numbers = string.Join('\n', clients.Select(c => $"`+{c.PhoneNumber}` ({c.Name})"));
        await _client.SendMessage(chat, $"💼 *Клієнти*\n{numbers}\nОберіть:", 
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
            _ => _client.SendMessage(message.Chat, "✖️ Немає такого варіанту вибору."),
        };
    }
}
