// One stack of goods in the player's trunk (PORTPLAN items 26/51, port of
// Unity PlayerData.trunkItems / StorageItem). Quantity is mutable because
// buying the same item merges into the stack.
namespace AceDrive.Core;

/// <summary>One trunk stack: an item's name, unit volume and quantity.</summary>
public sealed record TrunkItem(string Name, double UnitVolume)
{
    /// <summary>Units carried; always positive for a live stack.</summary>
    public int Quantity { get; set; }
}
