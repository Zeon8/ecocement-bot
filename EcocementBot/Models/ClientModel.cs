using EcocementBot.Data.Enums;

namespace EcocementBot.Models;

public class ClientModel
{
    public string PhoneNumber { get; set; }

    public string Name { get; set; }

    public string Address { get; set; }

    public ClientPaymentType PaymentType { get; set; }
}
