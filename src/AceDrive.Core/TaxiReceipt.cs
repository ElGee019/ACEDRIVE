// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11 (Unity
// TaxiReceiptScreen lines: distance, fare tiers, forfait, tip, speed
// fines, total earned, XP + XP calculation).
namespace AceDrive.Core;

/// <summary>
/// One settled taxi job, ready to render. All money in EUR, already
/// rounded to cents by <see cref="TaxiFare"/>. The friendship tail is the
/// Unity TaxiJobEarnings.customerName + friendshipTierIncreased pair:
/// empty name means the passenger matched no known resident, so the
/// receipt hides the box.
/// </summary>
public sealed record TaxiReceipt(
    double DistanceKm,
    int Capacity,
    IReadOnlyList<TaxiReceiptTier> TierLines,
    double DistanceCharge,
    double Forfait,
    double BaseTotal,
    double Tip,
    int Fines,
    double FineDeduction,
    double Total,
    double XpEarned,
    int CarClass,
    int CarRarity,
    int StreetKnowledgePct,
    string FriendshipName,
    int FriendshipLevel,
    bool FriendshipTierIncreased)
{
    /// <summary>False when the passenger matched no known resident.</summary>
    public bool HasFriendship => FriendshipName.Length > 0;
}
