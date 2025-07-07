using System.Collections.Concurrent;

namespace EcocementBot.Services;

public class SessionService
{
    public Dictionary<long, string> Sessions { get; set; } = new();

    public void Authorize(long userId, string phoneNumber) 
        => Sessions[userId] = phoneNumber;

    public string GetPhoneNumber(long userId) => Sessions[userId];

    public bool IsAuthorized(long userId) => Sessions.ContainsKey(userId);
}
