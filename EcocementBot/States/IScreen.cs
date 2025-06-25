using Telegram.Bot.Types;

namespace EcocementBot.States;

public interface IScreen
{
    Task EnterAsync(User user, Chat chat);

    Task HandleInput(Message message);
}
