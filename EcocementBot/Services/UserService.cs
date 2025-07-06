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

    public Task UpdateTelegramUserId(string phoneNumber, long telegramUserId)
    {
        return _context.Users
            .Where(u => u.PhoneNumber == phoneNumber)
            .ExecuteUpdateAsync(o => o.SetProperty(u => u.TelegramUserId, telegramUserId));
    }

    public Task UpdateUserPhone(string oldPhoneNumber, string newPhoneNumber)
    {
        return _context.Users
            .Where(u => u.PhoneNumber == oldPhoneNumber)
            .ExecuteUpdateAsync(o => o.SetProperty(u => u.PhoneNumber, newPhoneNumber));
    }

    public async Task CreateUser(string phoneNumber)
    {
        await _context.Users.AddAsync(new User
        {
            PhoneNumber = phoneNumber,
        });

        await _context.SaveChangesAsync();
    }

    public async Task DeleteUser(string phoneNumber)
    {
        await _context.Users
            .Where(u => u.PhoneNumber == phoneNumber)
            .ExecuteDeleteAsync();
    }
}
