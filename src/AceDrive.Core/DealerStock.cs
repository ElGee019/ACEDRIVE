// The car dealer's weekly floor (PORTPLAN item 29, port of Unity
// Utils/CarDealerSelectionService). Pure data selection: no money moves here, the
// purchase itself is Garage + DriverWallet.
//
// Unity parity, kept: one deterministic draw per week (same week, same floor,
// nothing persisted), the partial Fisher-Yates pick, the ordinal sort, and the
// owned exclusion *before* picking — a sold-out roster therefore shows fewer
// cars instead of refilling.
//
// Divergence, deliberate: Unity splits the roster into "built this year" (max 8)
// and "older" and seeds with year * 10007 + week * 131. ACEDRIVE has no year
// system and never will — the week is the only pacing axis — so the band and the
// year half of the seed are gone and the whole roster is one pool. Divergence,
// documented: the pool order is the catalogue's row order, so the draw is tied to
// Data/cars.json's row order like Unity's is tied to its database asset.
namespace AceDrive.Core;

public static class DealerStock
{
    /// <summary>
    /// This week's floor: up to <see cref="DealerRules.MaxShownCars"/> cars the
    /// player does not own yet, drawn deterministically from <paramref name="week"/>,
    /// ordered by <see cref="CarSpec.Key"/>. Rows the game does not sell
    /// (<see cref="CarSpec.Price"/> &lt;= 0) never appear.
    /// </summary>
    public static IReadOnlyList<CarSpec> Build(
        IReadOnlyList<CarSpec> pool, IReadOnlySet<string> ownedKeys, int week)
    {
        List<CarSpec> sellable = [];
        foreach (CarSpec car in pool)
        {
            if (car.Price > 0.0 && !ownedKeys.Contains(car.Key))
            {
                sellable.Add(car);
            }
        }

        var rng = new Random(DealerRules.SeedFor(week));
        int count = Math.Min(DealerRules.MaxShownCars, sellable.Count);
        List<CarSpec> shown = new(count);
        // Unity's PickDistinct: swap a random survivor into the window head.
        // Only the head is consumed, so the tail stays arbitrary — same result
        // for the same pool order and seed, which is what the tests pin.
        for (int i = 0; i < count; i++)
        {
            int j = rng.Next(i, sellable.Count);
            (sellable[i], sellable[j]) = (sellable[j], sellable[i]);
            shown.Add(sellable[i]);
        }

        shown.Sort(static (a, b) => string.CompareOrdinal(a.Key, b.Key));
        return shown;
    }
}
