using EcocementBot.Data;
using EcocementBot.Data.Entities;
using EcocementBot.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace EcocementBot.Services;

public class MarkService
{
    private readonly ApplicationDbContext _context;

    public MarkService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<string>> GetMarks()
    {
        return await _context.Marks.AsNoTracking().Select(m => m.Name).ToArrayAsync();
    }

    public async Task CreateMark(string name)
    {
        var exists = await _context.Marks.AnyAsync(m => m.Name == name);
        if (exists)
            throw new MarkExistsException();

        var mark = new Mark
        {
            Name = name,
        };

        _context.Marks.Add(mark);
        await _context.SaveChangesAsync();
    }

    public async Task RemoveMark(string name)
    {
        var mark = await _context.Marks.FindAsync(name) 
            ?? throw new MarkNotExistException();

        _context.Marks.Remove(mark);
        await _context.SaveChangesAsync();
    }
}
