// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The outcome of one reward claim (PORTPLAN item 61).
namespace AceDrive.Core;

/// <summary>
/// What one claim did: where the reward went, the cash a bonus card hands the
/// wallet and the capacity a slot card leaves behind. The delivered page prints
/// the readings; the reward desk applies them.
/// </summary>
public sealed record RewardClaim(
    RewardClaimStatus Status,
    double Cash = 0.0,
    int GloveboxSlots = 0)
{
    /// <summary>False only for a full glovebox: nothing moved, nothing to save.</summary>
    public bool Applied => Status != RewardClaimStatus.GloveboxFull;
}
