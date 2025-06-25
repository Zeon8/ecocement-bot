using EcocementBot.Data;
using Microsoft.EntityFrameworkCore;

namespace EcocementBot.Services;

public class UserService
{
    private readonly ApplicationDbContext _context;

    public UserService(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<bool> IsAdministrator(long userId) => _context.Administrators.AnyAsync(a => a.Id == userId);
}
