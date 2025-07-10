using EcocementBot.Data.Enums;
using System.Text.Json.Serialization;

namespace EcocementBot.Models;

public record CarDeliveryTime(TimeOfDay TimeOfDay, TimeOnly? CustomTime = null);

[JsonDerivedType(typeof(WithinDay), typeDiscriminator: nameof(WithinDay))]
[JsonDerivedType(typeof(Individual), typeDiscriminator: nameof(Individual))]
public abstract record OrderCarTime
{
    public record WithinDay : OrderCarTime;

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
    Anytime,
    Custom,
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

    public required OrderCarTime CarTime { get; init; }

    public OrderPaymentType PaymentType { get; init; }
}
