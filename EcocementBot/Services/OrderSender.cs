using EcocementBot.Data.Enums;
using EcocementBot.Models;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace EcocementBot.Services;

public class OrderSender
{
    public long GroupId { get; set; }

    private readonly TelegramBotClient _client;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    private static readonly CultureInfo s_culture = new CultureInfo("uk-UA");

    private static readonly Dictionary<OrderPaymentType, string> s_playmentTypeStrings = new()
    {
        [OrderPaymentType.Cashless] = "Безготівкова",
        [OrderPaymentType.Cash] = "Готівка",
    };

    private static readonly Dictionary<TimeOfDay, string> s_timeOfTheDayStrings = new()
    {
        [TimeOfDay.Morning] = "Ранок",
        [TimeOfDay.Day] = "Обід",
        [TimeOfDay.Evening] = "Вечір",
        [TimeOfDay.Anytime] = "Протягом дня",
    };

    public OrderSender(TelegramBotClient client, 
        IConfiguration configuration, 
        IServiceProvider serviceProvider)
    {
        _client = client;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    public async Task Send(User user, OrderModel model)
    {
        string message = await FormatOrder(user, model);
        await _client.SendMessage(new ChatId(GroupId), message);
    }

    private async Task<string> FormatOrder(User user, OrderModel model)
    {
        await using var scoped = _serviceProvider.CreateAsyncScope();
        var clientService = scoped.ServiceProvider.GetRequiredService<ClientService>();
        var userService = scoped.ServiceProvider.GetRequiredService<UserService>();

        var phoneNumber = await userService.GetPhoneNumber(user.Id);
        var client = await clientService.GetClient(phoneNumber!);

        StringBuilder builder = new();

        var date = model.Date.ToString(s_culture);
        var dateOfWeek = s_culture.DateTimeFormat.GetDayName(model.Date.DayOfWeek);

        builder.AppendLine($"Дата: {date} ({dateOfWeek})");
        builder.AppendLine($"Замовник: {client!.Name}");
        builder.AppendLine($"Адреса: {client.Address}");
        builder.AppendLine($"Цемент: {model.Mark}");

        string receiveType = model.ReceiveType switch
        {
            OrderReceivingType.Delivery => "Доставка",
            OrderReceivingType.SelfPickup => "Самовивіз"
        };

        builder.AppendLine($"Спосіб отримання: {receiveType}");
        builder.AppendLine($"Форма оплати: {s_playmentTypeStrings[model.PaymentType]}");

        builder.AppendLine($"Кількість авто: {model.CarsCount}");
        builder.AppendLine();

        if (model.CarTime is OrderCarTime.Individual individual)
        {
            for (int i = 0; i < individual.CarTimes.Count; i++)
            {
                CarDeliveryTime CarDeliveryTime = individual.CarTimes[i];
                builder.AppendLine($"Авто №{i + 1}: {ToTimeOnly(CarDeliveryTime)}");
            }
        }
        else if(model.CarTime is OrderCarTime.WithinDay)
        {
            for (int i = 0; i < model.CarsCount; i++)
                builder.AppendLine($"Авто №{i + 1}: Протягом дня");
        }

        return builder.ToString();
    }

    private static string ToTimeOnly(CarDeliveryTime CarDeliveryTime)
    {
        if (CarDeliveryTime.TimeOfDay == TimeOfDay.Custom)
            return CarDeliveryTime.CustomTime!.Value.ToString(s_culture)!;

        return s_timeOfTheDayStrings[CarDeliveryTime.TimeOfDay];
    }
}
