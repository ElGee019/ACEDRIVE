// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// What claiming one reward card did (PORTPLAN item 61, port of Unity
// TodoRewardsScreenController.OnRewardButtonClicked).
namespace AceDrive.Core;

/// <summary>How one reward claim settled.</summary>
public enum RewardClaimStatus
{
    /// <summary>The card is now in the glove compartment.</summary>
    Stored,

    /// <summary>The glove compartment holds one more card.</summary>
    SlotAdded,

    /// <summary>The cash bonus is ready to bank (the wallet owns the money).</summary>
    CashCredited,

    /// <summary>No free slot, so nothing moved and the other reward stays offered.</summary>
    GloveboxFull,
}
