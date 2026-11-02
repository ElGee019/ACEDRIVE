// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The draw behind the care-package picker (PORTPLAN item 61, port of Unity
// TodoRewardsScreenController.RandomizeButtons).
namespace AceDrive.Core;

/// <summary>
/// Draws the two rewards a picker page offers. Pure over a seeded stream, so a
/// test can pin the offer the way the other launch draws are pinned.
/// </summary>
public static class RewardDraw
{
    /// <summary>
    /// Two distinct cards from the catalogue, or null when it cannot fill a
    /// page (fewer than two rewards, the shape a stripped data file takes). The
    /// second index is drawn over the remaining slots and shifted past the
    /// first, which is Unity's "remove the pick, draw again" without the list
    /// copy.
    /// </summary>
    public static RewardOffer? Offer(RewardTable table, Random rng)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(rng);

        IReadOnlyList<RewardCard> cards = table.Cards;
        if (cards.Count < 2)
        {
            return null;
        }

        int first = rng.Next(cards.Count);
        int second = rng.Next(cards.Count - 1);
        if (second >= first)
        {
            second++;
        }

        return new RewardOffer(cards[first], cards[second]);
    }
}
