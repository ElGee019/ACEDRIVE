// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: Unity CarHaulOfferService.TryBuildVehicleOffer (loyalty gate)
// and PlayerData.lastCarHaulGameDay/lastCarHaulYear (one haul per day),
// PORTPLAN item 20.
namespace AceDrive.Core;

/// <summary>
/// The two rules in front of a haul offer, kept switchable while the job is
/// being built: the brand-loyalty floor and Unity's one-haul-per-game-day
/// stamp. <see cref="Development"/> is the build shipped today (no floor, no
/// limit) and <see cref="Unity"/> is the mother game's release reading, so
/// turning the rules on later is a one-line change at the composition root
/// with no branch anywhere in the flow.
/// </summary>
public sealed record CarHaulPolicy(bool RequireLoyalty, bool OneHaulPerGameDay)
{
    /// <summary>
    /// Unity's release gate: a brand must reach 100% loyalty before its
    /// customers' new cars can be hauled (the shipped source carries a 0%
    /// testing override, which is not ported).
    /// </summary>
    public const double UnlockLoyaltyPercent = 100.0;

    /// <summary>Today's build: any brand the player has driven, no daily cap.</summary>
    public static readonly CarHaulPolicy Development = new(
        RequireLoyalty: false, OneHaulPerGameDay: false);

    /// <summary>Unity's release rules: 100% loyalty and one haul per game day.</summary>
    public static readonly CarHaulPolicy Unity = new(
        RequireLoyalty: true, OneHaulPerGameDay: true);

    /// <summary>
    /// True when the brand's loyalty percentage opens offers. Without the
    /// floor, a brand still has to have been driven at all (Unity's override
    /// keeps that part of the gate): the mileage adapter answers 0 for a brand
    /// the player never drove, and 0 never unlocks.
    /// </summary>
    public bool UnlocksOffer(double loyaltyPercent)
    {
        if (!double.IsFinite(loyaltyPercent) || loyaltyPercent <= 0.0)
        {
            return false;
        }

        return !RequireLoyalty || loyaltyPercent >= UnlockLoyaltyPercent;
    }

    /// <summary>
    /// True when a new offer may be drawn now. Unity stamps the game day (and
    /// year) only when a haul is delivered, so a replacement haul may be
    /// offered the next day. An in-flight haul always blocks a new offer — that
    /// guard is the port's, not Unity's (the source would overwrite the active
    /// job), and it holds even while the daily cap is off.
    /// </summary>
    public bool CanOffer(
        DateTime gameNow, DateTime? lastDeliveredOn, bool haulInProgress) =>
        !haulInProgress
        && (!OneHaulPerGameDay
            || lastDeliveredOn is not { } last
            || last.Date != gameNow.Date);
}
