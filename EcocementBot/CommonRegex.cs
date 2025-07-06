using System.Text.RegularExpressions;

namespace EcocementBot;

public partial class CommonRegex
{
    public static Regex PhoneNumber => GetPhoneNumber();

    [GeneratedRegex("^\\+380\\d{9}$")]
    private static partial Regex GetPhoneNumber();
}
