// Pure gameplay draws for the Saturday Touristenfahrten delivery run. Every
// random step takes the caller's seeded instance (never the shared global), so
// a test pins the run and a launch still surprises the player.
namespace AceDrive.Core;

/// <summary>
/// The three draws one run needs: which pickup shop kind a fresh door is (and
/// its company name), which 1-3 dropoffs the run visits, and the order each
/// dropoff takes. Quantity ranges and the two-item floor come from the menu
/// data; nothing here hardcodes a number beyond the math itself.
/// </summary>
public static class TouristenRules
{
    /// <summary>A pickup kind to open a fresh shop door for, or null with no rows.</summary>
    public static TouristenPickup? DrawPickup(TouristenMenu menu, Random rng)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(rng);

        return menu.Pickups is { Count: > 0 } pickups
            ? pickups[rng.Next(pickups.Count)]
            : null;
    }

    /// <summary>
    /// One company name for a freshly opened pickup door. The definition's own
    /// display name is the fallback when the row names none.
    /// </summary>
    public static string PickupName(TouristenPickup pickup, Random rng)
    {
        ArgumentNullException.ThrowIfNull(pickup);
        ArgumentNullException.ThrowIfNull(rng);

        return pickup.Names is { Count: > 0 } names
            ? names[rng.Next(names.Count)]
            : pickup.PoiType;
    }

    /// <summary>
    /// The run's dropoff rows: between the menu's minimum and maximum, drawn
    /// without repeats so one corner is never visited twice in the same run.
    /// Empty when the menu names no place or its bounds are unusable.
    /// </summary>
    public static IReadOnlyList<TouristenDropoffDefinition> DrawDropoffs(
        TouristenMenu menu, Random rng)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(rng);

        if (menu.Dropoffs is not { Count: > 0 } places)
        {
            return [];
        }

        int max = Math.Min(Math.Max(1, menu.MaxDropoffs), places.Count);
        int min = Math.Clamp(menu.MinDropoffs, 1, max);
        int count = min >= max ? max : rng.Next(min, max + 1);

        // Partial Fisher-Yates over a copy: the first `count` slots are the
        // draw, every place equally likely and never repeated.
        var pool = new List<TouristenDropoffDefinition>(places);
        for (int i = 0; i < count; i++)
        {
            int pick = rng.Next(i, pool.Count);
            (pool[i], pool[pick]) = (pool[pick], pool[i]);
        }

        var drawn = new List<TouristenDropoffDefinition>(count);
        for (int i = 0; i < count; i++)
        {
            drawn.Add(pool[i]);
        }

        return drawn;
    }

    /// <summary>
    /// One dropoff's order: every item rolls its own range, then the draw tops
    /// the order up to <paramref name="minItems"/> by adding units to random
    /// items that still have room (the pizza's guaranteed slice means it only
    /// needs one more). A range table with no capacity left stops the top-up
    /// instead of looping.
    /// </summary>
    public static TouristenOrder DrawOrder(
        TouristenPickup pickup, int minItems, Random rng)
    {
        ArgumentNullException.ThrowIfNull(pickup);
        ArgumentNullException.ThrowIfNull(rng);

        if (pickup.Items is not { Count: > 0 } rules)
        {
            return new TouristenOrder([]);
        }

        var lines = new List<TouristenOrderLine>(rules.Count);
        int total = 0;
        foreach (TouristenItemRule rule in rules)
        {
            int quantity = rng.Next(rule.Min, rule.Max + 1);
            lines.Add(new TouristenOrderLine(rule.Key, rule.NameKey, quantity, rule.Price));
            total += quantity;
        }

        int floor = Math.Max(0, minItems);
        while (total < floor)
        {
            int start = rng.Next(lines.Count);
            bool grown = false;
            for (int step = 0; step < lines.Count; step++)
            {
                int index = (start + step) % lines.Count;
                if (lines[index].Quantity >= rules[index].Max)
                {
                    continue;
                }

                lines[index] = lines[index] with { Quantity = lines[index].Quantity + 1 };
                total++;
                grown = true;
                break;
            }

            if (!grown)
            {
                break;
            }
        }

        return new TouristenOrder(lines);
    }
}
