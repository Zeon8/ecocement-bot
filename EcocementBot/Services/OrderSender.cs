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
    private readonly TelegramBotClient _client;
    private readonly SessionService _sessionService;
    private readonly ClientService _clientService;
    private readonly IConfiguration _configuration;

    private static CultureInfo _ukrCultureInfo = new CultureInfo("uk-UA");

    private static readonly Dictionary<PaymentType, string> s_playmentTypeStrings = new()
    {
        [PaymentType.Card] = "Безготівкова",
        [PaymentType.Cash] = "Готівка",
    };

    private static readonly Dictionary<TimeOfDay, string> s_timeOfTheDayStrings = new()
    {
        [TimeOfDay.Morning] = "Ранок",
        [TimeOfDay.Day] = "Обід",
        [TimeOfDay.Evening] = "Вечір",
    };

    public OrderSender(TelegramBotClient client, SessionService sessionService, ClientService clientService, IConfiguration configuration)
    {
        _client = client;
        _sessionService = sessionService;
        _clientService = clientService;
        _configuration = configuration;
    }

    public async Task Send(User user, OrderModel model)
    {
        var groupId = _configuration["GroupId"]
            ?? throw new InvalidOperationException("GroupId not found in configuration file.");

        string message = await FormatOrder(user, model);
        await _client.SendMessage(new ChatId(groupId), message);
    }

    private async Task<string> FormatOrder(User user, OrderModel model)
    {
        var phoneNumber = _sessionService.GetPhoneNumber(user.Id);
        var client = await _clientService.GetClient(phoneNumber);

        StringBuilder builder = new();

        var date = model.Date.ToString(_ukrCultureInfo);
        var dateOfWeek = _ukrCultureInfo.DateTimeFormat.GetDayName(model.Date.DayOfWeek);
        builder.AppendLine($"Дата: {date} ({dateOfWeek})");

        builder.AppendLine($"Замовник: {client!.Name}");
        builder.AppendLine($"Адреса: {client.Address}");
        

        string carsExpression;
        if (model.CarsCount == 1)
        {
            string carOwners = model.ReceiveType == OrderReceivingType.Delivery ? "НАША" : "ЇХНЯ";
            carsExpression = $"{carOwners} машина";
        }
        else
        {
            string carOwners = model.ReceiveType == OrderReceivingType.Delivery ? "НАШИХ" : "ЇХ";
            carsExpression = $"{carOwners} машин";
        }

        builder.AppendLine($"Авто: {model.CarsCount} {carsExpression}");
        if (model.CarTime is OrderCarTime.General general)
            builder.AppendLine($"Загальний час автівок: {ToTimeOnly(general.Time)}");

        builder.AppendLine($"Цемент: {model.Mark}");

        builder.AppendLine($"Форма оплати: {s_playmentTypeStrings[model.PaymentType]}");

        if (model.CarTime is OrderCarTime.Individual individual)
        {
            builder.AppendLine();
            for (int i = 0; i < individual.CarTimes.Count; i++)
            {
                CarDeliveryTime CarDeliveryTime = individual.CarTimes[i];
                builder.AppendLine($"Авто №{i + 1}: {ToTimeOnly(CarDeliveryTime)}");
            }
        }

        return builder.ToString();
    }

    private static string ToTimeOnly(CarDeliveryTime CarDeliveryTime)
    {
        if (CarDeliveryTime.TimeOfDay == TimeOfDay.Custom)
            return CarDeliveryTime.CustomTime!.Value.ToString(CultureInfo.CurrentUICulture)!;

        return s_timeOfTheDayStrings[CarDeliveryTime.TimeOfDay];
    }
}
