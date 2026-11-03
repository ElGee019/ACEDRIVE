// One stack of goods in the player's trunk (PORTPLAN items 26/51, port of
// Unity PlayerData.trunkItems / StorageItem). Quantity is mutable because
// buying the same item merges into the stack. A counter-limited reward card is
// a stack too (0 L keepsake) whose UsesLeft/UsesMax pair is the countdown the
// trunk page prints and every completed job spends.
namespace AceDrive.Core;

/// <summary>One trunk stack: an item's name, unit volume and quantity.</summary>
public sealed record TrunkItem(string Name, double UnitVolume)
{
    /// <summary>Units carried; always positive for a live stack.</summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Charges a counter-limited card has left. 0 on goods and on cards whose
    /// effect has no countdown.
    /// </summary>
    public int UsesLeft { get; set; }

    /// <summary>
    /// The card's charge limit as issued, printed beside <see cref="UsesLeft"/>.
    /// 0 on goods and on cards without a counter.
    /// </summary>
    public int UsesMax { get; set; }
}
