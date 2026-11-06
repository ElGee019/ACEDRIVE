namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Where Friday's pancake visit stands. Session state, never persisted: the week
/// is only stamped once the meal is actually served, so a process that dies on
/// the drive leaves the row playable (toast and all), exactly like the
/// missing-person run's phases.
/// </summary>
public enum PancakePhase
{
    /// <summary>No visit in flight: the calendar row can open the briefing.</summary>
    Idle,

    /// <summary>The briefing is up; the player has not started the drive yet.</summary>
    Briefing,

    /// <summary>Grandma's door is pinned and the companion guides the drive.</summary>
    Driving,

    /// <summary>The meal is served and the receipt is up; CONTINUE returns to the menu.</summary>
    Served,
}
