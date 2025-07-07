using EcocementBot.Data.Enums;
using System.Text.Json.Serialization;

namespace EcocementBot.Models;

public record CarDeliveryTime(TimeOfDay TimeOfDay, TimeOnly? CustomTime = null);

[JsonDerivedType(typeof(General), typeDiscriminator: nameof(General))]
[JsonDerivedType(typeof(Individual), typeDiscriminator: nameof(Individual))]
public abstract record OrderCarTime
{
    public record General(CarDeliveryTime Time) : OrderCarTime;

    public record Individual : OrderCarTime
    {
        public List<CarDeliveryTime> CarTimes { get; set; } = new();
    }
}

public enum TimeOfDay
{
    Morning,
    Day,
    Evening,
    Custom
}

public enum OrderReceivingType
{
    Delivery,
    SelfPickup
}

public class OrderModel
{
    public DateOnly Date { get; init; }

    public OrderReceivingType ReceiveType { get; init; }

    public required string Mark { get; init; }

    public int CarsCount { get; init; }

    public OrderCarTime CarTime { get; init; }

    public PaymentType PaymentType { get; init; }
}
