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
    private readonly ClientService _clientService;

    private static readonly KeyboardButton _cancelButton = new KeyboardButton("🚫 Скасувати");
    private static readonly ReplyKeyboardMarkup _cancelKeyboard = new()
    {
        Keyboard = [[_cancelButton]]
    };

    public DeleteClientScreen(TelegramBotClient client, Navigator navigator, ClientService clientService)
    {
        _client = client;
        _navigator = navigator;
        _clientService = clientService;
    }

    public Task EnterAsync(User user, Chat chat)
    {
        return _client.SendMessage(chat, "*🗑 Видалення клієнта*\n\nВведіть номер клієнта:",
            parseMode: ParseMode.Markdown,
            replyMarkup: _cancelKeyboard);
    }

    public async Task HandleInput(Message message)
    {
        if(message.Text == _cancelButton.Text)
        {
            await _navigator.PopScreen(message.From!, message.Chat);
            return;
        }

        string phoneNumber = message.Text;
        try
        {
            await _clientService.DeleteClient(phoneNumber);
            
        }
        catch(ClientNotFoundException)
        {
            await _client.SendMessage(message.Chat, "✖️ Клієнта з таким номером не знайдено.\n\n Введіть номер клієнта:");
            return;
        }

        await _client.SendMessage(message.Chat, "Клієнта видалено ✅.");
        await _navigator.PopScreen(message.From!, message.Chat);
    }
}
