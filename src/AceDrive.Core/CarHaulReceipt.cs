// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 20 (Unity
// CarHaulingScreen lines: car, drive time, earning, driver rating, tier,
// rate, dealer-owner familiarity).
namespace AceDrive.Core;

/// <summary>
/// One settled car-hauling delivery, ready to render. All money in EUR,
/// already rounded to cents by <see cref="CarHaulReceiptBuilder"/>. Unlike
/// the taxi and delivery receipts there is no XP tail: Unity's settle adds
/// the cash and nothing else, so the receipt carries no XP lines either.
/// The friendship tail is the Unity dealer-owner pair (empty name means no
/// dealer POI behind the job, so the receipt hides the box — while the map
/// has no dealers, every haul hits that branch).
/// </summary>
public sealed record CarHaulReceipt(
    string CarName,
    int Tier,
    int DriverRating,
    double RatePerMin,
    double TotalEarned,
    int DriveMinutes,
    string DealerOwnerName = "",
    int DealerFriendshipLevel = 0,
    bool DealerFriendshipTierIncreased = false)
{
    /// <summary>Unity's hours + minutes pair, split here so the resx
    /// pluralised wording stays the page's business.</summary>
    public int DriveHours => DriveMinutes / 60;

    /// <summary>Minutes remainder after whole hours.</summary>
    public int DriveMinutesRemainder => DriveMinutes % 60;

    /// <summary>False when the job had no dealer POI to credit.</summary>
    public bool HasFriendship => DealerOwnerName.Length > 0;
}
