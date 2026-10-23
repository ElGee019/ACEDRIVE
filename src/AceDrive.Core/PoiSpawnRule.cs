// The pacing numbers behind one generated business family (PORTPLAN item 24,
// port of Unity CarWashGenerator/BankGenerator/InsuranceBrokerGenerator/
// CarDealerGenerator/CarMechanicGenerator). Pure data: the spawner compares
// the saved street/job counters against it and never hardcodes a threshold.
namespace AceDrive.Core;

/// <summary>
/// When the n-th business of one family may open. The record lives on
/// <see cref="PoiDefinition.Spawn"/> so the content file owns the balance and
/// the spawner reads one table.
/// </summary>
/// <param name="Pacing">The curve this family follows.</param>
/// <param name="FirstStreets">Streets needed for the first street-paced
/// business (also the offset the later intervals count from).</param>
/// <param name="IntervalStreets">Further streets needed per additional
/// business.</param>
/// <param name="FirstJobs">Settled jobs needed for a job-gated opening.</param>
public sealed record PoiSpawnRule(
    PoiPacing Pacing = PoiPacing.None,
    int FirstStreets = 0,
    int IntervalStreets = 0,
    int FirstJobs = 0)
{
    /// <summary>
    /// How many businesses of this family should exist at the given progress.
    /// Negative or missing counters answer zero. The curves are Unity's
    /// generators kept shape-for-shape:
    /// <list type="bullet">
    /// <item>streets: 1 at <see cref="FirstStreets"/>, then one per interval;</item>
    /// <item>jobs-first: the job-gated first, then the street intervals;</item>
    /// <item>either: the larger of the job-gated first and the street curve;</item>
    /// <item>owned category: one immediately, then one per interval, and none
    /// until the hub category is owned.</item>
    /// </list>
    /// </summary>
    /// <param name="categoryOwned">Whether the definition's hub category is
    /// owned; only <see cref="PoiPacing.OwnedCategory"/> reads it.</param>
    public int ExpectedCount(
        int discoveredStreets, int settledJobs, bool categoryOwned = true)
    {
        int streets = Math.Max(0, discoveredStreets);
        int jobs = Math.Max(0, settledJobs);
        bool jobOpening = FirstJobs > 0 && jobs >= FirstJobs;

        return Pacing switch
        {
            PoiPacing.Streets => StreetOpening(streets),
            PoiPacing.JobsFirstThenStreets => jobOpening
                ? 1 + StreetIntervals(streets)
                : 0,
            PoiPacing.Either => Math.Max(
                jobOpening ? 1 : 0, StreetOpening(streets)),
            PoiPacing.OwnedCategory => categoryOwned && IntervalStreets > 0
                ? Math.Max(1, streets / IntervalStreets)
                : 0,
            _ => 0,
        };
    }

    // Unity's RequiredStreetCountFor(count) = first + interval * count, read
    // backwards: the first door opens at FirstStreets, every later one adds
    // one interval.
    private int StreetOpening(int streets) =>
        FirstStreets > 0 && IntervalStreets > 0 && streets >= FirstStreets
            ? 1 + (streets - FirstStreets) / IntervalStreets
            : 0;

    // Unity's mechanic curve counts the intervals AFTER the job-gated first:
    // the first street-paced extra needs FirstStreets + one full interval.
    private int StreetIntervals(int streets) =>
        FirstStreets >= 0 && IntervalStreets > 0 && streets > FirstStreets
            ? (streets - FirstStreets) / IntervalStreets
            : 0;
}
