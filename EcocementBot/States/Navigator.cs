using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types;

namespace EcocementBot.States;

public class Navigator
{
    public Dictionary<long, Stack<IScreen>> Screens { get; set; } = new();

    private readonly IServiceProvider _services;

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
        if (!Screens.TryGetValue(user.Id, out Stack<IScreen>? stack))
        {
            stack = new Stack<IScreen>();
            Screens[user.Id] = stack;
        }
        stack.Push(screen);
        return screen.EnterAsync(user, chat);
    }

    public bool TryGetScreen(User user, [NotNullWhen(returnValue: true)] out IScreen? screen)
    {
        if (Screens.TryGetValue(user.Id, out var stack))
        {
            screen = stack.Peek();
            return true;
        }

        screen = null;
        return false;
    }

    public IScreen? GetScreen(User user)
    {
        if(Screens.TryGetValue(user.Id, out var stack))
            return stack.Peek();
        return null;
    }

    public Task GoBack(User user, Chat chat)
    {
        Screens[user.Id].Pop();
        return Screens[user.Id].Peek().EnterAsync(user, chat);
    }

    public void Clear(TelegramUser user)
    {
        if (Screens.TryGetValue(user.Id, out Stack<IScreen>? stack))
            stack.Clear();
    }
}
