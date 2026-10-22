using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Parsing the shared <c>players</c> board once for the XP, zone and shift
/// standings. Skips the local player's own doc and rows without the field.
/// </summary>
public static class PlayerBoard
{
    public static List<double> OtherXp(
        IReadOnlyList<FirestoreDocument> documents, string? ownUid)
    {
        List<double> values = [];
        foreach (FirestoreDocument doc in documents)
        {
            if (IsOwn(doc, ownUid)) continue;
            if (doc.Number("xp") is { } xp) values.Add(xp);
        }
        return values;
    }

    public static List<double> OtherZoneKm(
        IReadOnlyList<FirestoreDocument> documents, string? ownUid)
    {
        List<double> values = [];
        foreach (FirestoreDocument doc in documents)
        {
            if (IsOwn(doc, ownUid)) continue;
            if (doc.Number("longestZoneKm") is { } km) values.Add(km);
        }
        return values;
    }

    public static List<ShiftBoardEntry> OtherShiftEntries(
        IReadOnlyList<FirestoreDocument> documents, string? ownUid)
    {
        List<ShiftBoardEntry> values = [];
        foreach (FirestoreDocument doc in documents)
        {
            if (IsOwn(doc, ownUid)) continue;
            if (doc.Number("highestShiftEarnings") is not { } earnings) continue;
            // WHY MaxValue: an absent stamp ranks BELOW every real stamp, so a
            // tie never lifts that player above one who earned it first
            // (Unity's fallback). Zero would win every tie instead.
            long first = doc.Timestamp("highestShiftEarningsFirstEarnedAt")?.Ticks
                ?? long.MaxValue;
            values.Add(new ShiftBoardEntry(earnings, first));
        }
        return values;
    }

    private static bool IsOwn(FirestoreDocument doc, string? ownUid) =>
        !string.IsNullOrEmpty(ownUid)
        && string.Equals(doc.Id, ownUid, StringComparison.Ordinal);
}
