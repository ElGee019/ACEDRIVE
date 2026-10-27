using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Maps Firestore documents from <c>acevo_enteredForValuation</c> into the
/// pure <see cref="ValuationEntry"/> the valuation flow compares. The Unity
/// field names are kept so a doc written by either client reads the same.
/// </summary>
public static class ValuationEntryMapper
{
    /// <summary>One document, or null when it has no usable identity.</summary>
    public static ValuationEntry? Map(FirestoreDocument document)
    {
        if (document is null || string.IsNullOrEmpty(document.Id))
        {
            return null;
        }

        string uid = document.Text("uid") is { Length: > 0 } fieldUid
            ? fieldUid
            : document.Id;
        return new ValuationEntry(
            uid,
            document.Text("playerName") ?? string.Empty,
            document.Text("carId") ?? string.Empty,
            Math.Max(0, document.IntValue("jobsWithCar") ?? 0),
            ClampPercent(document.Number("engineBrokenPercent")),
            Math.Max(0.0, document.Number("score") ?? 0.0),
            document.Timestamp("enteredAt"));
    }

    /// <summary>Every document that maps, in list order.</summary>
    public static IReadOnlyList<ValuationEntry> MapAll(
        IReadOnlyList<FirestoreDocument>? documents)
    {
        List<ValuationEntry> entries = [];
        if (documents is null)
        {
            return entries;
        }
        foreach (FirestoreDocument document in documents)
        {
            if (Map(document) is { } entry)
            {
                entries.Add(entry);
            }
        }
        return entries;
    }

    private static double ClampPercent(double? value) =>
        double.IsFinite(value ?? 0.0) ? Math.Clamp(value ?? 0.0, 0.0, 1.0) : 0.0;
}
