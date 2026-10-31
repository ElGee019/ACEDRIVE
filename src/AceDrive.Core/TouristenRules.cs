// Pure gameplay draws for the Saturday Touristenfahrten delivery run. Every
// random step takes the caller's seeded instance (never the shared global), so
// a test pins the run and a launch still surprises the player.
namespace AceDrive.Core;

/// <summary>
/// The four draws one run needs: which pickup shop kind a fresh door is (and
/// its company name), which 1-3 dropoffs the run visits, the one job order the
/// shop loads, and the split of that order into one share per dropoff. Quantity
/// ranges and the per-dropoff floor come from the menu data; nothing here
/// hardcodes a number beyond the math itself.
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
    /// The run's one job order: every item rolls its own range, then the draw
    /// tops the order up to <paramref name="minItems"/> by adding units to
    /// random items that still have room (the pizza's guaranteed slice means it
    /// only needs one more). A range table with no capacity left stops the
    /// top-up instead of looping. The caller passes the floor for the whole
    /// load, so every dropoff still clears the menu's per-dropoff minimum once
    /// <see cref="SplitOrder"/> divides it.
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

    /// <summary>
    /// Divides the run's one job order across <paramref name="parts"/> dropoffs
    /// the way a shop would pack it: one unit per stop first, so no stop is told
    /// to deliver nothing while the load can fill it, then every remaining unit
    /// goes to a randomly drawn stop. The shares are deliberately uneven — one
    /// corner may get most of the load, the next a couple of items — and only
    /// the line identity (key, name key, unit price) is kept per unit; the
    /// quantities are what split. A load smaller than the dropoff list leaves
    /// the last shares empty, and the caller skips those stops.
    /// </summary>
    public static IReadOnlyList<TouristenOrder> SplitOrder(
        TouristenOrder order, int parts, Random rng)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(parts);

        var shares = new List<TouristenOrderLine>[parts];
        for (int part = 0; part < parts; part++)
        {
            shares[part] = [];
        }

        var units = new List<TouristenOrderLine>();
        foreach (TouristenOrderLine line in order.Lines)
        {
            for (int unit = 0; unit < line.Quantity; unit++)
            {
                units.Add(line);
            }
        }

        // The guaranteed one-per-stop pass is what keeps every driven stop in
        // the run once the load is big enough; everything after it is the
        // random deal that makes the shares uneven.
        int guaranteed = Math.Min(parts, units.Count);
        for (int part = 0; part < guaranteed; part++)
        {
            AddUnit(shares[part], units[part]);
        }

        for (int unit = guaranteed; unit < units.Count; unit++)
        {
            AddUnit(shares[rng.Next(parts)], units[unit]);
        }

        var split = new List<TouristenOrder>(parts);
        foreach (List<TouristenOrderLine> share in shares)
        {
            split.Add(new TouristenOrder(share));
        }

        return split;
    }

    // One unit onto a share, merged into the line it belongs to: a share lists
    // each item once, in the order the deal first reached it.
    private static void AddUnit(List<TouristenOrderLine> share, TouristenOrderLine line)
    {
        for (int i = 0; i < share.Count; i++)
        {
            if (string.Equals(share[i].Key, line.Key, StringComparison.Ordinal))
            {
                share[i] = share[i] with { Quantity = share[i].Quantity + 1 };
                return;
            }
        }

        share.Add(line with { Quantity = 1 });
    }
}
