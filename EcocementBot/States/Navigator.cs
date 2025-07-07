using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types;

namespace EcocementBot.States;

public class Navigator
{
    public IReadOnlyDictionary<long, Stack<IScreen>> Screens => _screens;

    private readonly IServiceProvider _services;
    private readonly Dictionary<long, Stack<IScreen>> _screens = new();

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
        if (!_screens.TryGetValue(user.Id, out Stack<IScreen>? stack))
        {
            stack = new Stack<IScreen>();
            _screens[user.Id] = stack;
        }
        stack.Push(screen);
        return screen.EnterAsync(user, chat);
    }

    public bool TryGetScreen(User user, [NotNullWhen(returnValue: true)] out IScreen? screen)
    {
        if (_screens.TryGetValue(user.Id, out var stack))
        {
            screen = stack.Peek();
            return true;
        }

        screen = null;
        return false;
    }

    public IScreen? GetScreen(User user)
    {
        if(_screens.TryGetValue(user.Id, out var stack))
            return stack.Peek();
        return null;
    }

    public Task GoBack(User user, Chat chat)
    {
        _screens[user.Id].Pop();
        return _screens[user.Id].Peek().EnterAsync(user, chat);
    }
}
