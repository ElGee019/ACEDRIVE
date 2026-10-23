using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Parsing the shared <c>players</c> board once for the XP, zone and shift
/// standings. Skips the local player's CURRENT username row and rows without
/// the field; an earlier username of the same uid stays on the board as a
/// rival, because the username is the world identity (one doc per name).
/// Legacy rows written before the name-keyed board used the anonymous uid as
/// the doc id. They are hidden only while they still carry the current
/// username; a legacy row recorded under an earlier name stays a rival, so a
/// failed one-time move cannot silently shrink the board to one player.
/// </summary>
public static class PlayerBoard
{
    public static List<double> OtherXp(
        IReadOnlyList<FirestoreDocument> documents, string? ownUid, string? ownName)
    {
        List<double> values = [];
        foreach (FirestoreDocument doc in documents)
        {
            if (IsOwn(doc, ownUid, ownName)) continue;
            if (doc.Number("xp") is { } xp) values.Add(xp);
        }
        return values;
    }

    public static List<double> OtherZoneKm(
        IReadOnlyList<FirestoreDocument> documents, string? ownUid, string? ownName)
    {
        List<double> values = [];
        foreach (FirestoreDocument doc in documents)
        {
            if (IsOwn(doc, ownUid, ownName)) continue;
            if (doc.Number("longestZoneKm") is { } km) values.Add(km);
        }
        return values;
    }

    public static List<ShiftBoardEntry> OtherShiftEntries(
        IReadOnlyList<FirestoreDocument> documents, string? ownUid, string? ownName)
    {
        List<ShiftBoardEntry> values = [];
        foreach (FirestoreDocument doc in documents)
        {
            if (IsOwn(doc, ownUid, ownName)) continue;
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

    private static bool IsOwn(FirestoreDocument doc, string? ownUid, string? ownName)
    {
        string ownKey = PlayerNameKey.Normalize(ownName);
        if (ownKey.Length > 0
            && string.Equals(doc.Id, ownKey, StringComparison.Ordinal))
        {
            return true;
        }

        // Legacy row from before the name-keyed board: its id is our uid.
        // Hide it only while it still carries our current name. A legacy row
        // recorded under an earlier username is the same player's old world
        // identity and must stay a rival even when the one-time move could not
        // run (for example while the name-keyed rules are not deployed yet).
        if (string.IsNullOrEmpty(ownUid)
            || !string.Equals(doc.Id, ownUid, StringComparison.Ordinal))
        {
            return false;
        }

        // No current name yet, or the legacy row has no recorded name: it is
        // still safest to treat it as our own pre-migration self.
        string legacyKey = PlayerNameKey.Normalize(doc.Text("displayName"));
        return ownKey.Length == 0 || legacyKey.Length == 0
            || string.Equals(legacyKey, ownKey, StringComparison.Ordinal);
    }
}
