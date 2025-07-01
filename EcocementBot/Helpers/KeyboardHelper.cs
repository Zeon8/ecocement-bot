using Telegram.Bot.Types.ReplyMarkups;

namespace EcocementBot.Helpers;

public static class KeyboardHelper
{
    public static List<IEnumerable<KeyboardButton>> CreateKeyboard(IReadOnlyList<string> buttons)
    {
        var keyboard = new List<IEnumerable<KeyboardButton>>();
        AddMarkButtons(keyboard, buttons);
        return keyboard;
    }

    private static void AddMarkButtons(List<IEnumerable<KeyboardButton>> keyboard, IReadOnlyList<string> marks)
    {
        const int buttonsInRow = 2;
        for (int i = 0; i < marks.Count; i += buttonsInRow)
        {
            int length = Math.Min(buttonsInRow, marks.Count - i);
            var buttons = new KeyboardButton[length];

            for (int j = 0; j < length; j++)
                buttons[j] = new KeyboardButton(marks[i + j]);

            keyboard.Add(buttons);
        }
    }
}
