using EcocementBot.Data;
using EcocementBot.Data.Entities;
using EcocementBot.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace EcocementBot.Services;

public class UserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<User?> GetUser(long userId) 
        => _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TelegramUserId == userId);

    public Task<User?> GetUser(string phoneNumber)
        => _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);

    public async Task UpdateTelegramUserId(string phoneNumber, long telegramUserId)
    {
        var user = await _context.Users.FindAsync([phoneNumber]) 
            ?? throw new UnauthorizedException();

        user!.TelegramUserId = telegramUserId;
        await _context.SaveChangesAsync();
    }
}
