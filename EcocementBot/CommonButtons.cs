using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot;

public class CommonButtons
{
    public static KeyboardButton CancelButton { get; } = new KeyboardButton("🚫 Скасувати");

    public static KeyboardButton BackButton { get; } = new("⬅️ Назад");

    public static KeyboardButton PreviousStepButton { get; } = new("↩️ Назад");
}
