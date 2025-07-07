using System.Text.RegularExpressions;

namespace EcocementBot;

public partial class CommonRegex
{
    public static Regex PhoneNumber => GetPhoneNumber();

    public static Regex NonDigitSymbol => GetNonDigitSymbol();

    [GeneratedRegex("^\\+380\\d{9}$")]
    private static partial Regex GetPhoneNumber();

    [GeneratedRegex(@"\D")]
    private static partial Regex GetNonDigitSymbol();
}
