using System.Text;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The Touristenfahrten task text: the dropoff names behind the pickup
/// sentence and one order's item list behind the dropoff sentence. Copy and
/// separators live in resx, so a translator owns both the wording and the
/// list punctuation; Core only supplies the item keys and quantities.
/// </summary>
internal static class TouristenCopy
{
    /// <summary>The run's dropoff names, e.g. "Brünnchen, Einfahrt and Pflanzgarten".</summary>
    public static string Names(IReadOnlyList<TouristenDropoff> dropoffs)
    {
        var names = new List<string>(dropoffs.Count);
        foreach (TouristenDropoff dropoff in dropoffs)
        {
            names.Add(dropoff.Door.Name);
        }

        return Join(names);
    }

    /// <summary>One order's lines, e.g. "2 × coffee, 1 × beer and 3 × schnitzel".</summary>
    public static string Order(TouristenOrder order)
    {
        var parts = new List<string>();
        foreach (TouristenOrderLine line in order.Lines)
        {
            if (line.Quantity > 0)
            {
                parts.Add(Strings.Format(
                    "TouristenItemLine", line.Quantity, Strings.Get(line.NameKey)));
            }
        }

        return Join(parts);
    }

    // One join for both sentences: the last pair reads with the localized
    // "and", every earlier pair with the localized comma.
    private static string Join(List<string> parts)
    {
        if (parts.Count == 0)
        {
            return string.Empty;
        }

        if (parts.Count == 1)
        {
            return parts[0];
        }

        string separator = Strings.Get("TouristenListSeparator");
        string and = Strings.Get("TouristenListAnd");
        var text = new StringBuilder();
        for (int i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                text.Append(i == parts.Count - 1 ? and : separator);
            }

            text.Append(parts[i]);
        }

        return text.ToString();
    }
}
