// Pure gameplay data for the Saturday Touristenfahrten run: the real-world
// Nürburgring calendar the briefing page's title and the run's race-weekend
// bonus read. Data/touristenweekends.json is the source of truth; None is
// the fallback when that file is missing or unusable.
namespace AceDrive.Core;

/// <summary>
/// The year's Nürburgring events, in calendar order, plus the payout factor a
/// delivery earns while one of them runs. The calendar is content, so the
/// dates and the event names live in the data file; the only math here is the
/// lookup (which event covers a date) and the factor it exposes.
/// </summary>
/// <param name="Weekends">The events, each with its own day range.</param>
/// <param name="BonusMultiplier">What a delivery pays while an event runs:
/// 1.0 leaves the price alone, 2.0 doubles the goods.</param>
public sealed record TouristenRaceCalendar(
    IReadOnlyList<TouristenRaceWeekend> Weekends,
    double BonusMultiplier)
{
    /// <summary>
    /// No race calendar: no event title and no bonus. The shipped default when
    /// the data file is missing, and what the tests that do not care hand in.
    /// </summary>
    public static TouristenRaceCalendar None { get; } = new([], 1.0);

    /// <summary>
    /// The event running on <paramref name="date"/>, or null on an ordinary
    /// day. First match wins, so an overlap is the earlier row's to report.
    /// </summary>
    public TouristenRaceWeekend? Find(DateOnly date)
    {
        if (Weekends is not { Count: > 0 } weekends)
        {
            return null;
        }

        foreach (TouristenRaceWeekend weekend in weekends)
        {
            if (weekend.Covers(date))
            {
                return weekend;
            }
        }

        return null;
    }

    /// <summary>
    /// What one delivered order pays on <paramref name="date"/>: the calendar's
    /// bonus while an event runs, the plain price otherwise. A malformed
    /// factor can only ever fall back to 1.0, never make a delivery cheaper.
    /// </summary>
    public double Multiplier(DateOnly date) =>
        Find(date) is null || !double.IsFinite(BonusMultiplier)
            ? 1.0
            : Math.Max(1.0, BonusMultiplier);

    /// <summary>
    /// True when the table can drive the title and the bonus: at least one
    /// event, a sane bonus and every row naming its days and itself. A
    /// hand-edited file that fails this falls back to <see cref="None"/>
    /// instead of titling a page with an empty name.
    /// </summary>
    public bool IsUsable
    {
        get
        {
            if (Weekends is not { Count: > 0 })
            {
                return false;
            }

            if (!double.IsFinite(BonusMultiplier) || BonusMultiplier < 1.0)
            {
                return false;
            }

            foreach (TouristenRaceWeekend weekend in Weekends)
            {
                if (string.IsNullOrWhiteSpace(weekend.Name) || weekend.End < weekend.Start)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
