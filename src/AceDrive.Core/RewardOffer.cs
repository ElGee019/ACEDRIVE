// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The two rewards one picker page offers (PORTPLAN item 61, port of Unity
// TodoRewardsScreenController's two ButtonCarnini copies).
namespace AceDrive.Core;

/// <summary>
/// The two distinct rewards a care-package picker offers. Unity draws two
/// different cards from the catalogue, so the page never shows the same reward
/// twice and the pick is a real choice.
/// </summary>
public sealed record RewardOffer(RewardCard First, RewardCard Second)
{
    /// <summary>The offer in page order.</summary>
    public IReadOnlyList<RewardCard> Cards => [First, Second];
}
