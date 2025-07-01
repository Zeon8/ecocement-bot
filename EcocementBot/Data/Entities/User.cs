using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EcocementBot.Data.Entities;

public enum UserType
{
    Client,
    Admin
}

public class User
{
    [Key]
    public required string PhoneNumber { get; set; }

    public required UserType UserType { get; set; }

    public long? TelegramUserId { get; set; }
}
