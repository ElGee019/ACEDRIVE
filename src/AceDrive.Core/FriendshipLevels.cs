// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 45 (Unity
// FriendshipHelper: tier bands + Signore Moro first-name rule).
namespace AceDrive.Core;

/// <summary>
/// Familiarity bands shared by the taxi receipt and (later) the POI
/// network. Levels start at 0; the first completed ride moves a new
/// resident to 1. Level 5 is the "friend" line everything else keys off.
/// </summary>
public static class FriendshipLevels
{
    /// <summary>First level that counts as a friend (Unity HUD + road-test rule).</summary>
    public const int FriendThreshold = 5;

    public static int GetTier(int level)
    {
        if (level <= 2) return 0;
        if (level <= 4) return 1;
        if (level <= 9) return 2;
        if (level <= 12) return 3;
        if (level <= 15) return 4;
        return 5;
    }

    public static bool TierIncreased(int before, int after) =>
        GetTier(after) > GetTier(before);

    /// <summary>
    /// First-name form for display: "Signore Moro" keeps the full name
    /// (Signore is a title, not a first name); everyone else uses the
    /// first token.
    /// </summary>
    public static string DisplayFirstName(string ownerName)
    {
        if (string.IsNullOrEmpty(ownerName)) return "";
        if (ownerName.StartsWith("Signore ", StringComparison.OrdinalIgnoreCase))
            return ownerName;
        int space = ownerName.IndexOf(' ');
        return space < 0 ? ownerName : ownerName.Substring(0, space);
    }
}
