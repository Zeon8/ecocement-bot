using EcocementBot.Data;
using EcocementBot.Data.Entities;
using EcocementBot.Exceptions;
using EcocementBot.Models;
using Microsoft.EntityFrameworkCore;

namespace EcocementBot.Services;

public class ClientService
{
    private readonly ApplicationDbContext _context;

    public ClientService(ApplicationDbContext context) => _context = context;

    public async Task CreateClient(ClientModel model)
    {
        var client = new Client
        {
            Name = model.Name,
            Address = model.Address,
            PaymentType = model.PaymentType,
            PhoneNumber = model.PhoneNumber,
        };

        await _context.Clients.AddAsync(client);
        await _context.SaveChangesAsync();
    }

    public Task<Client?> GetClient(string phoneNumber) 
        => _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);

    public async Task UpdateClient(ClientModel model)
    {
        var client = await _context.Clients.FindAsync(model.PhoneNumber)
             ?? throw new ClientNotFoundException(model.PhoneNumber);

        client.PhoneNumber = model.PhoneNumber;
        client.Name = model.Name;
        client.Address = model.Address;
        client.PaymentType = model.PaymentType;

         await _context.SaveChangesAsync();
    }

    public async Task DeleteClient(string phoneNumber)
    {
        var client = await _context.Clients.FindAsync(phoneNumber)
            ?? throw new ClientNotFoundException(phoneNumber);

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync();
    }
}
