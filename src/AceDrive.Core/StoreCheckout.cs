// Pure shop transaction (PORTPLAN item 26, port of Unity
// MaxsScreen.OnBuyItem): validate stock, cash and trunk space, then move the
// goods, the money and the stock together. The page only calls Quote for its
// readout and Buy for the tap.
namespace AceDrive.Core;

/// <summary>The one place a shop purchase is priced and applied.</summary>
public static class StoreCheckout
{
    /// <summary>
    /// Prices a proposed purchase without moving anything. The caller uses
    /// the status as the BUY gate and the numbers for the selection panel.
    /// </summary>
    public static StorePurchaseQuote Quote(
        StoreInventory inventory, Trunk trunk, DriverWallet wallet,
        StoreItem item, int quantity, double trunkCapacity)
    {
        double remaining = trunk.Remaining(trunkCapacity);
        if (quantity <= 0)
        {
            return new StorePurchaseQuote(
                StorePurchaseStatus.InvalidQuantity, quantity, 0.0, 0.0, remaining);
        }

        double totalPrice = Math.Round(
            item.Price * quantity, 2, MidpointRounding.AwayFromZero);
        double totalVolume = item.Volume * quantity;
        double projected = remaining - totalVolume;
        StorePurchaseStatus status =
            inventory.StockOf(item.Name) < quantity ? StorePurchaseStatus.NotEnoughStock
            : totalPrice > wallet.Cash ? StorePurchaseStatus.NotEnoughCash
            : projected < 0.0 ? StorePurchaseStatus.TrunkFull
            : StorePurchaseStatus.Success;

        return new StorePurchaseQuote(
            status, quantity, totalPrice, totalVolume, projected);
    }

    /// <summary>
    /// Applies a legal purchase; a failed quote leaves cash, stock and trunk
    /// untouched. Callers persist the wallet/trunk through their own save.
    /// </summary>
    public static StorePurchaseQuote Buy(
        StoreInventory inventory, Trunk trunk, DriverWallet wallet,
        StoreItem item, int quantity, double trunkCapacity)
    {
        StorePurchaseQuote quote = Quote(
            inventory, trunk, wallet, item, quantity, trunkCapacity);
        if (quote.Status != StorePurchaseStatus.Success)
        {
            return quote;
        }

        // Spend first so a refused take can be refunded; the quote already
        // proved both, but neither step may leave the other half-applied.
        if (!wallet.TrySpend(quote.TotalPrice))
        {
            return quote with { Status = StorePurchaseStatus.NotEnoughCash };
        }
        if (!inventory.TryTake(item.Name, quantity))
        {
            // Unreachable after the quote's stock check. AddBonus is the
            // wallet's only credit path; it is a plain refund here.
            wallet.AddBonus(quote.TotalPrice);
            return quote with { Status = StorePurchaseStatus.NotEnoughStock };
        }
        trunk.Add(item, quantity);
        return quote;
    }
}
