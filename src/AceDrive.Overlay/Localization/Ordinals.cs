using System.Globalization;

namespace AceDrive.Overlay.Localization;

/// <summary>
/// English ordinal numbers for the standings line ("3rd", "11th"): a port
/// of Unity StringHelper.Ordinal. English-only by decision — the resx owns
/// the sentence, this owns only the suffix rule. Non-positive numbers get
/// the bare digits, like Unity.
/// </summary>
public static class Ordinals
{
    public static string Of(int num)
    {
        if (num <= 0)
        {
            return num.ToString(CultureInfo.CurrentCulture);
        }

        if (num % 100 is >= 11 and <= 13)
        {
            return num + "th";
        }

        return (num % 10) switch
        {
            1 => num + "st",
            2 => num + "nd",
            3 => num + "rd",
            _ => num + "th",
        };
    }
}
