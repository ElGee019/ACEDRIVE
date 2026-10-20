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
/// receipt hides the box. The brand tail is the Unity sponsorship pair:
/// empty brand (or zero km) means no sponsorship, so the receipt hides
/// those lines like any other zero row.
/// </summary>
public sealed record TaxiReceipt(
    double DistanceKm,
    int Capacity,
    IReadOnlyList<ReceiptTier> TierLines,
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
    bool FriendshipTierIncreased,
    string BrandName = "",
    double BrandDistanceKm = 0.0,
    double BrandBonusRate = 0.0,
    double BrandBonus = 0.0)
{
    /// <summary>False when the passenger matched no known resident.</summary>
    public bool HasFriendship => FriendshipName.Length > 0;

    /// <summary>False when the ride earned no brand sponsorship.</summary>
    public bool HasBrandBonus => BrandBonus > 0.0;
}
