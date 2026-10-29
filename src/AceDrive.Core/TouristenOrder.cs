// Pure gameplay data for the Saturday Touristenfahrten delivery run.
namespace AceDrive.Core;

/// <summary>
/// One dropoff's loaded order: the item lines the shop handed over. The money
/// that settles is the sum of the line prices, computed here once so the
/// receipt and any later reading can never disagree. An order always holds at
/// least <see cref="TouristenMenu.MinItemsPerOrder"/> items, enforced by
/// <see cref="TouristenRules.DrawOrder"/>.
/// </summary>
public sealed record TouristenOrder(IReadOnlyList<TouristenOrderLine> Lines)
{
    /// <summary>Total units in the order (zero-quantity lines excluded).</summary>
    public int ItemCount
    {
        get
        {
            int count = 0;
            foreach (TouristenOrderLine line in Lines)
            {
                if (line.Quantity > 0)
                {
                    count += line.Quantity;
                }
            }

            return count;
        }
    }

    /// <summary>What the customer pays for the goods, rounded to cents.</summary>
    public double Total
    {
        get
        {
            double total = 0.0;
            foreach (TouristenOrderLine line in Lines)
            {
                if (line.Quantity > 0)
                {
                    total += line.Quantity * line.UnitPrice;
                }
            }

            return Math.Round(total, 2, MidpointRounding.AwayFromZero);
        }
    }
}
