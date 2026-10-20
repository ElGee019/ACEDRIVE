// One live shelf in a shop (PORTPLAN items 25/26, port of Unity StoreSlot):
// the item plus how many are left. The page reads it; StoreInventory owns
// the mutation so stock can never underflow.
namespace AceDrive.Core;

/// <summary>One item's live stock on a shop shelf.</summary>
public sealed class StoreShelf
{
    public StoreShelf(StoreItem item, int quantity)
    {
        Item = item;
        Quantity = Math.Max(0, quantity);
    }

    public StoreItem Item { get; }

    public int Quantity { get; private set; }

    /// <summary>Takes stock; false (and no move) when the shelf is short.</summary>
    internal bool TryTake(int quantity)
    {
        if (quantity <= 0 || quantity > Quantity)
        {
            return false;
        }
        Quantity -= quantity;
        return true;
    }
}
