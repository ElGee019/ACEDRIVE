// Where one road test is (PORTPLAN item 49). Session state only: Unity never
// persists a half-driven test either — a restart just leaves the car untested.
namespace AceDrive.Overlay.ViewModels;

/// <summary>The road test's phase, from the calendar tap to the result page.</summary>
public enum RoadTestPhase
{
    /// <summary>No test is running; the calendar row is the only entry.</summary>
    Idle,

    /// <summary>The companion guides the player to La Strada.</summary>
    Driving,

    /// <summary>The page is open at the test centre, no leg started yet.</summary>
    AtTestCentre,

    /// <summary>
    /// The start counter (and its hold) runs on the companion. It waits at
    /// five while the car is rolling and restarts from five once it is halted.
    /// </summary>
    Countdown,

    /// <summary>The 0-60 stopwatch runs; the next 60 km/h crossing ends the leg.</summary>
    Timing,

    /// <summary>
    /// The brake leg waits for the car to reach 100 km/h; nothing is measured
    /// before that.
    /// </summary>
    BrakeApproach,

    /// <summary>
    /// The brake leg is measuring: the distance from 100 km/h to the halt.
    /// </summary>
    BrakeTiming,

    /// <summary>
    /// The photo mission is live: the panel is down and the companion offers
    /// the PHOTOSHOOT button wherever the player drives. The shot is taken from
    /// the car's live position, so the leg ends where the player stops.
    /// </summary>
    PhotoMission,

    /// <summary>The finish sentence and the car's board are up.</summary>
    Result,

    /// <summary>
    /// The photo leg settled: the receipt page is up and the visit is over. Its
    /// CONTINUE is the way out (Unity returns to the map there).
    /// </summary>
    Receipt,
}
