using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics.CodeAnalysis;
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

    public Task Open<T>(User user, Chat chat) where T: notnull, IScreen
    {
        var screen = _services.GetRequiredService<T>();
        return Open(screen, user, chat);
    }

    public Task Open(IScreen screen, User user, Chat chat)
    {
        if (!_screenStacks.TryGetValue(user.Id, out Stack<IScreen>? stack))
        {
            stack = new Stack<IScreen>();
            _screenStacks[user.Id] = stack;
        }
        stack.Push(screen);
        return screen.EnterAsync(user, chat);
    }

    public bool TryGetScreen(User user, [NotNullWhen(returnValue: true)] out IScreen? screen)
    {
        if (_screenStacks.TryGetValue(user.Id, out var stack))
        {
            screen = stack.Peek();
            return true;
        }

        screen = null;
        return false;
    }

    public IScreen? GetScreen(User user)
    {
        if(_screenStacks.TryGetValue(user.Id, out var stack))
            return stack.Peek();
        return null;
    }

    public Task GoBack(User user, Chat chat)
    {
        _screenStacks[user.Id].Pop();
        return _screenStacks[user.Id].Peek().EnterAsync(user, chat);
    }
}
