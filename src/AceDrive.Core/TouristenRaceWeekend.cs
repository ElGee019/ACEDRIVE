// Pure gameplay data for the Saturday Touristenfahrten run: one real-world
// Nürburgring event and the days its spectators are at the track. No WPF, no
// System.IO, no real-time source — the caller hands the date in.
namespace AceDrive.Core;

/// <summary>
/// One real-world event at the Nürburgring: the calendar days it runs (a
/// single race day or a multi-day meeting, both ends included) and the name
/// the run's briefing page wears while it is on. The dates are the player's
/// real-world calendar dates, never game time, because the crowd the run
/// feeds is the real one.
/// </summary>
public sealed record TouristenRaceWeekend(DateOnly Start, DateOnly End, string Name)
{
    /// <summary>True on every day the event is listed for, both ends included.</summary>
    public bool Covers(DateOnly date) => date >= Start && date <= End;
}
