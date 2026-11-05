// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 44 (the driver's
// attentive-driver reading) and item 06 (the skills it pays).
namespace AceDrive.Core;

/// <summary>
/// The driver's career mileage record: the kilometres every finished job
/// banked, the clean run since the last billed crash, the longest clean run,
/// and the crash count. The wallet owns one and only the wallet writes it; the
/// snapshot carries the readings so a restart resumes the same record.
///
/// One bank per finished job is the whole point: the meter compares the car's
/// odometer at the job's start against the reading at its settle, so nothing
/// here runs on the 10 Hz poll. Every <see cref="AwardKilometres"/> of a clean
/// run still pays one random skill point (Unity's ApplyRandomSkillDelta), and a
/// crash drops the run but keeps the record.
/// </summary>
public sealed class DrivingRecord
{
    /// <summary>The clean distance one random skill point costs (PORTPLAN 44:
    /// every 5 km a finished job banks without an accident adds 1 skill point).</summary>
    public const double AwardKilometres = 5.0;

    /// <summary>WHY a cap: a hand-edited save must not mint a burst of skill
    /// points on one settle. A real job is a few kilometres, so this cap is far
    /// above anything the simulation can produce.</summary>
    private const int MaxAwardsPerBank = 1000;

    public DrivingRecord(
        double jobDistanceKm = 0.0,
        double accidentFreeKm = 0.0,
        double longestAccidentFreeKm = 0.0,
        int accidents = 0)
    {
        JobDistanceKm = NonNegative(jobDistanceKm);
        AccidentFreeKm = NonNegative(accidentFreeKm);
        // The record can never be shorter than the live run: a save written
        // between the bank and the record's write would otherwise go backwards.
        LongestAccidentFreeKm = Math.Max(NonNegative(longestAccidentFreeKm), AccidentFreeKm);
        Accidents = Math.Max(0, accidents);
    }

    /// <summary>Kilometres banked by every finished job (taxi, delivery, haul).</summary>
    public double JobDistanceKm { get; private set; }

    /// <summary>Kilometres driven since the last billed crash.</summary>
    public double AccidentFreeKm { get; private set; }

    /// <summary>The longest clean run ever banked.</summary>
    public double LongestAccidentFreeKm { get; private set; }

    /// <summary>Billed crashes over the career.</summary>
    public int Accidents { get; private set; }

    /// <summary>
    /// The attentive-driver percentage: the career's km per crash, capped at
    /// 100. One hundred clean kilometres per crash therefore reads 100 %, and
    /// a career that has never crashed opens on 100 % (the design's "no
    /// accidents yet" reading). Below that the percentage falls with each
    /// crash; a crash before any job has banked reads 0 %.
    /// </summary>
    public double AttentiveRating =>
        Accidents == 0
            ? 100.0
            : Math.Min(100.0, JobDistanceKm / Accidents);

    /// <summary>
    /// Banks one finished job's distance and answers how many skill points the
    /// clean run earned. Non-positive and non-finite readings are ignored, so a
    /// torn odometer can never move the record.
    /// </summary>
    public int Bank(double km)
    {
        if (!double.IsFinite(km) || km <= 0.0)
        {
            return 0;
        }

        double before = AccidentFreeKm;
        JobDistanceKm += km;
        AccidentFreeKm += km;
        LongestAccidentFreeKm = Math.Max(LongestAccidentFreeKm, AccidentFreeKm);
        // The difference of the two awards keeps a run that crosses the
        // threshold across two jobs honest (4.9 km + 0.1 km still pays once).
        double earned = Math.Floor(AccidentFreeKm / AwardKilometres)
            - Math.Floor(before / AwardKilometres);
        return (int)Math.Clamp(earned, 0.0, MaxAwardsPerBank);
    }

    /// <summary>
    /// A billed crash ends the clean run but keeps its record: the run is
    /// folded into the longest reading before it resets.
    /// </summary>
    public void RegisterAccident()
    {
        LongestAccidentFreeKm = Math.Max(LongestAccidentFreeKm, AccidentFreeKm);
        AccidentFreeKm = 0.0;
        if (Accidents < int.MaxValue)
        {
            Accidents++;
        }
    }

    private static double NonNegative(double value) =>
        double.IsFinite(value) && value > 0.0 ? value : 0.0;
}
