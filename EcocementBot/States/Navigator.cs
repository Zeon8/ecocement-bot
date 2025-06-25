using System;
using Telegram.Bot.Types;

namespace EcocementBot.States;

public class Navigator
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<long, Stack<IScreen>> _screenStacks = new();

    public Navigator(IServiceProvider services)
    {
        _services = services;
    }

    public Task PushScreen<T>(User user, Chat chat) where T: notnull, IScreen
    {
        var screen = _services.GetRequiredService<T>();
        return PushScreen(screen, user, chat);
    }

    public Task PushScreen(IScreen screen, User user, Chat chat)
    {
        if (!_screenStacks.TryGetValue(user.Id, out Stack<IScreen>? stack))
        {
            stack = new Stack<IScreen>();
            _screenStacks[user.Id] = stack;
        }
        stack.Push(screen);
        return screen.EnterAsync(user, chat);
    }

    public IScreen? PeekScreen(User user)
    {
        if(_screenStacks.TryGetValue(user.Id, out var stack))
            return stack.Peek();
        return null;
    }

    public Task PopScreen(User user, Chat chat)
    {
        _screenStacks[user.Id].Pop();
        return _screenStacks[user.Id].Peek().EnterAsync(user, chat);
    }
}
