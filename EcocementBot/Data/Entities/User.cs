using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EcocementBot.Data.Entities;

public enum UserRole
{
    Client,
    Admin
}

public class User
{
    [Key]
    public required string PhoneNumber { get; set; }

    public UserRole Role { get; set; } = UserRole.Client;

    public long? TelegramUserId { get; set; }
}
