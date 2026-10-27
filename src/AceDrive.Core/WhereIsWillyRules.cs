// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: ToWhereIsWilly.md §2.1/§2.5/§3.1
// (Unity WhereIsWillyHandler.CalculateReward, JobSelectionScreen's Wednesday
// gate and JobSelectionScreen.HasCoffeeBeansCareer).
namespace AceDrive.Core;

/// <summary>
/// The weekly missing-person run's balance: the finder's fee (a share of the
/// garage's average catalogue value), its two multipliers and the 100 CR
/// floor, plus the Wednesday 08:00-20:00 gate and the two lookups the draw
/// and the city need. Shipped values live in Data/willy_rules.json; this
/// default is the test-shell fallback and the deterministic test fixture.
/// </summary>
public sealed record WhereIsWillyRules(
    double FinderFeeRate,
    double MinReward,
    double AttemptBase,
    double AttemptFloor,
    IReadOnlyList<WhereIsWillyShopBand> ShopBands)
{
    /// <summary>
    /// Unity's coffee career id (Data/delivery_hubs.json): owning any hub of
    /// this category opens the town's coffee shops and, with them, the event.
    /// The id is content, so it lives here beside the rules that read it.
    /// </summary>
    public const string CoffeeCareerId = "coffee_beans";

    /// <summary>Fallback used when the data file is missing or invalid.</summary>
    public static readonly WhereIsWillyRules Default = new(
        FinderFeeRate: 0.04,
        MinReward: 100.0,
        AttemptBase: 3.0,
        AttemptFloor: 0.5,
        ShopBands:
        [
            new(2, 1.0),
            new(5, 1.25),
            new(9, 1.5),
            new(14, 1.75),
            new(null, 2.0),
        ]);

    /// <summary>
    /// The table is usable only when every number is finite, the two
    /// multipliers can never reach zero and the bands ascend to exactly one
    /// open-ended row. A file that fails this degrades to
    /// <see cref="Default"/> instead of pricing a quest to nothing.
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (!double.IsFinite(FinderFeeRate) || FinderFeeRate <= 0.0
                || !double.IsFinite(MinReward) || MinReward < 0.0
                || !double.IsFinite(AttemptBase) || AttemptBase < 0.0
                || !double.IsFinite(AttemptFloor) || AttemptFloor <= 0.0
                || ShopBands is not { Count: > 0 })
            {
                return false;
            }

            int previousMax = -1;
            for (int i = 0; i < ShopBands.Count; i++)
            {
                WhereIsWillyShopBand? band = ShopBands[i];
                if (band is null || !double.IsFinite(band.Multiplier) || band.Multiplier <= 0.0)
                {
                    return false;
                }

                bool last = i == ShopBands.Count - 1;
                if (band.MaxShops is { } max)
                {
                    // A closed band must cover at least its predecessor and
                    // cannot sit behind an open-ended one.
                    if (last || max <= previousMax)
                    {
                        return false;
                    }
                    previousMax = max;
                }
                else if (!last)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Unity's shop-count band: the first band whose ceiling covers the town.</summary>
    public double ShopMultiplier(int shopCount)
    {
        int count = Math.Max(0, shopCount);
        foreach (WhereIsWillyShopBand band in ShopBands)
        {
            if (band.MaxShops is not { } max || count <= max)
            {
                return band.Multiplier;
            }
        }

        // Unreachable with a valid table (the last band is open-ended); a
        // hand-edited list must still answer a price instead of throwing.
        return ShopBands.Count > 0 ? ShopBands[^1].Multiplier : 1.0;
    }

    /// <summary>
    /// Unity's attempt multiplier: three for a first-try find, one less per
    /// failed shop, floored at 0.5, and never above the town's shop count (a
    /// one-shop town always multiplies by 1.0).
    /// </summary>
    public double AttemptMultiplier(int shopCount, int attempts) =>
        Math.Min(
            Math.Max(0, shopCount),
            Math.Max(AttemptFloor, AttemptBase - Math.Max(0, attempts)));

    /// <summary>
    /// Unity's CalculateReward: the average car value times the fee rate, then
    /// both multipliers, rounded to whole CR with banker's rounding
    /// (<c>Mathf.RoundToInt</c> is <c>Math.Round</c>, never away from zero),
    /// and never below the floor.
    /// </summary>
    public int Reward(double averageCarValue, int shopCount, int attempts)
    {
        double average = double.IsFinite(averageCarValue) ? Math.Max(0.0, averageCarValue) : 0.0;
        double baseReward = average * FinderFeeRate;
        double raw = baseReward
            * AttemptMultiplier(shopCount, attempts)
            * ShopMultiplier(shopCount);
        int fee = (int)Math.Round(double.IsFinite(raw) ? raw : 0.0, MidpointRounding.ToEven);
        return Math.Max(fee, Math.Max(0, (int)Math.Round(MinReward, MidpointRounding.ToEven)));
    }

    /// <summary>
    /// Mean catalogue price of the owned cars; unpriced rows (Price &lt;= 0)
    /// are skipped, exactly like Unity's average. 0 when the garage is empty
    /// or every car is priceless, which the floor then lifts to 100 CR.
    /// </summary>
    public static double AverageCarValue(IEnumerable<CarSpec?> cars)
    {
        if (cars is null)
        {
            return 0.0;
        }

        double total = 0.0;
        int priced = 0;
        foreach (CarSpec? car in cars)
        {
            if (car is null || !double.IsFinite(car.Price) || car.Price <= 0.0)
            {
                continue;
            }
            total += car.Price;
            priced++;
        }

        return priced == 0 ? 0.0 : total / priced;
    }

    /// <summary>
    /// The Wednesday number, read off the calendar data like the other events
    /// read theirs: the strip owns which weekday hosts the event.
    /// </summary>
    public static int DayOfWeek
    {
        get
        {
            foreach (CalendarDay day in ControlCalendar.Week)
            {
                if (day.EventId == ControlCalendar.Willy)
                {
                    return day.DayOfWeek;
                }
            }

            return -1;
        }
    }

    /// <summary>Unity OnWhereIsWillyClicked: Wednesday only, 08:00 &lt;= hour &lt; 20:00.</summary>
    public static bool IsWindow(int dayOfWeek, int hour) =>
        dayOfWeek == DayOfWeek && ControlCalendar.IsEventWindow(hour);

    /// <summary>
    /// Unity IsHomeAddress: the same house number on the same street, the
    /// street compared ignoring case and the side ignored (it is derived from
    /// the number anyway).
    /// </summary>
    public static bool SameDoor(AddressLocation a, AddressLocation b) =>
        a.HouseNumber == b.HouseNumber
        && string.Equals(a.StreetName.Trim(), b.StreetName.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Unity HasCoffeeBeansCareer: a coffee-shop door the player has discovered
    /// or a coffee-beans hub owned. False only locks the calendar row's label;
    /// the click still runs its own shop-count checks.
    /// </summary>
    public static bool HasCoffeeCareer(PoiDirectory directory, DeliveryCareer career)
    {
        if (career.OwnsCategory(CoffeeCareerId))
        {
            return true;
        }

        foreach (PointOfInterest poi in directory.All)
        {
            if (string.Equals(poi.Type, PoiTypes.CoffeeShop, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
