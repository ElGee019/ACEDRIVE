// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 16 (Unity
// DeliveryReceiptScreen lines + DeliveryJobHandler.ApplyStoreDeliveryEffects
// money block).
namespace AceDrive.Core;

/// <summary>
/// One settled delivery run, ready to render. All money in EUR, already
/// rounded to cents. The friendship tail carries the producing hub's owner
/// (assigned at hub purchase, Unity WholesaleHub.ownerName): empty means
/// the run rested on no owned hub, so the receipt hides the block. The
/// wallet delta is <see cref="TotalEarned"/> itself: the wholesale cost
/// left with the run and the retail payout plus the fare came back, a
/// positive earned is the run's profit added to the cash.
/// </summary>
public sealed record DeliveryReceipt(
    double DistanceKm,
    IReadOnlyList<ReceiptTier> TierLines,
    double DistanceCharge,
    double PaidForGoods,
    double ReceivedForGoods,
    double TotalEarned,
    double XpEarned,
    int StreetKnowledgePct,
    string HubOwnerName,
    int FriendshipLevel,
    bool FriendshipTierIncreased,
    int CredPEarned,
    double TotalCredP)
{
    /// <summary>False when the run rested on no owned hub.</summary>
    public bool HasFriendship => HubOwnerName.Length > 0;

    /// <summary>False when the stores rested on no owned hub.</summary>
    public bool HasCredP => CredPEarned > 0;
}
