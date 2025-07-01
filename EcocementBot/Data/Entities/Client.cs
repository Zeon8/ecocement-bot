using EcocementBot.Data.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EcocementBot.Data.Entities;

public class Client
{
    [Key]
    public required string PhoneNumber { get; set; }

    public required string Name { get; set; } 

    public required string Address { get; set; }

    public required PaymentType PaymentType { get; set; }
}
