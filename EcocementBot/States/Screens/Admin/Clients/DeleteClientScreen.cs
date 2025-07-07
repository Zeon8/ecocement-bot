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
    private readonly UserService _userService;

    public DeleteClientScreen(TelegramBotClient client, 
        Navigator navigator, 
        ClientService clientService, 
        UserService userService)
    {
        _client = client;
        _navigator = navigator;
        _clientService = clientService;
        _userService = userService;
    }

    public async Task EnterAsync(User user, Chat chat)
    {
        IEnumerable<string> phoneNumbers = await _clientService.GetPhoneNumbers();
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

        string phoneNumber = message.Text[1..]; // Skip +
        try
        {
            await _clientService.DeleteClient(phoneNumber);
        }
        catch(ClientNotFoundException)
        {
            await _client.SendMessage(message.Chat, "✖️ Клієнта з таким номером не знайдено.\n\n Введіть номер клієнта:");
            return;
        }

        await _userService.DeleteUser(phoneNumber);

        await _client.SendMessage(message.Chat, "Клієнта видалено ✅.");
        await _navigator.GoBack(message.From!, message.Chat);
    }
}
