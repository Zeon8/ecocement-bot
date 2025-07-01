using System.Collections.Concurrent;

namespace EcocementBot.Services;

public class SessionService
{
    private ConcurrentDictionary<long, string> _sessions = new();

    public void Authorize(long userId, string phoneNumber) => _sessions[userId] = phoneNumber;

    public string GetPhoneNumber(long userId) => _sessions[userId];

    public bool IsAuthorized(long userId) => _sessions.ContainsKey(userId);
}
