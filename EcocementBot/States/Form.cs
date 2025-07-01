using Telegram.Bot.Types;

namespace EcocementBot.States;

public abstract class Form<TContext, TStep> : IScreen where TStep : IStepScreen<TContext>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Stack<IStepScreen<TContext>> _steps = new();

    public Form(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public TContext GetContext();

    public Task EnterAsync(TelegramUser user, Chat chat)
    {
        if(_steps.TryPeek(out IStepScreen<TContext>  stepScreen))
        {
            stepScreen.Open(chat, user, );
        }

        var step = _serviceProvider.GetRequiredService<TStep>();
        _steps.Push(step);
        return
    }

    public Task HandleInput(Message message)
    {
        throw new NotImplementedException();
    }
}
