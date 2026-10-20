// Dealer stock rules (PORTPLAN item 29, port of Unity
// Utils/CarDealerSelectionService.cs). Balance numbers live here rather than in
// the generator, the BankRules precedent, so the dealer page and its tests read
// one source. The parade of the week is the only pacing axis ACEDRIVE has: there
// is no year system, so Unity's "brand new this year vs older" band and its
// year half of the seed are dropped (see DealerStock).
namespace AceDrive.Core;

public static class DealerRules
{
    /// <summary>How many cars one week's floor shows (Unity MaxShownCars).</summary>
    public const int MaxShownCars = 14;

    /// <summary>Unity seeds the week's draw with <c>year * 10007 + week * 131</c>;
    /// with no year system only the per-week stride survives, so consecutive
    /// weeks land far apart in the sequence.</summary>
    public const int WeekSeedStride = 131;

    /// <summary>Unity's dealer POI opens only after this many completed jobs
    /// (CarDealerGenerator.FirstDealerJobs). Dormant until the marker network
    /// (items 24/59) lands; the constant keeps the gate out of generator code.</summary>
    public const int FirstDealerJobs = 10;

    /// <summary>Then one dealer per this many discovered streets (Unity
    /// CarDealerGenerator). Parked with <see cref="FirstDealerJobs"/>.</summary>
    public const int DealerIntervalStreets = 37;

    /// <summary>Deterministic seed for one week's draw. Week is 1-based; a
    /// week below 1 (a hand-edited save) is clamped so the seed stays on the
    /// sequence instead of collapsing to the same value as week 0.</summary>
    public static int SeedFor(int week) => Math.Max(1, week) * WeekSeedStride;

    /// <summary>How many dealer POIs the discovered streets support (Unity's
    /// <c>streetCount / CarDealerIntervalStreets</c>): the first one needs
    /// <see cref="FirstDealerJobs"/> jobs, the rest one per interval.</summary>
    public static int DealersForStreets(int discoveredStreetCount) =>
        Math.Max(0, discoveredStreetCount) / DealerIntervalStreets;
}
