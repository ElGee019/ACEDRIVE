using System.Globalization;

namespace AceDrive.Overlay.Services;

/// <summary>
/// One Firestore document as the online layer reads it: the doc id plus its
/// fields decoded to CLR primitives (string, double, long, bool, DateTime,
/// dictionary, list, null). Data only; no HTTP concerns.
/// </summary>
public sealed record FirestoreDocument(
    string Id, IReadOnlyDictionary<string, object?> Fields)
{
    public string? Text(string field) =>
        Fields.TryGetValue(field, out object? value) ? value as string : null;

    public double? Number(string field)
    {
        if (!Fields.TryGetValue(field, out object? value) || value is null)
        {
            return null;
        }
        return value switch
        {
            double number => number,
            long number => number,
            int number => number,
            string text when double.TryParse(
                text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) => parsed,
            _ => null,
        };
    }

    public long? Whole(string field)
    {
        if (!Fields.TryGetValue(field, out object? value) || value is null)
        {
            return null;
        }
        return value switch
        {
            long number => number,
            int number => number,
            double number => (long)number,
            string text when long.TryParse(
                text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) => parsed,
            _ => null,
        };
    }

    public int? IntValue(string field) => Whole(field) is { } value ? (int)value : null;

    public DateTime? Timestamp(string field)
    {
        if (!Fields.TryGetValue(field, out object? value) || value is null)
        {
            return null;
        }
        return value switch
        {
            DateTime stamp => stamp,
            string text when DateTime.TryParse(
                text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out DateTime parsed) => parsed,
            _ => null,
        };
    }
}
