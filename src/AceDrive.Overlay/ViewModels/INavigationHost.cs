namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the coordinator the navigator reports to: the four entries
/// whose work is not a page — closing a shift that already timed out (the
/// coordinator owns the shift record and its summary page), re-reading the
/// readouts after a shift starts, and the settings and haul pages, which belong
/// to the story flow and the job settle rather than to navigation.
/// </summary>
public interface INavigationHost
{
    /// <summary>
    /// An idle taxi shift ended on its own (Unity's 30-minute rule). The
    /// coordinator banks the record and opens the shift-ended page.
    /// </summary>
    void ShiftTimedOut(TimeSpan idle);

    /// <summary>The job or shift state moved: the readouts are stale.</summary>
    void ReadoutsChanged();

    /// <summary>Opens the settings page (the story flow's preferences writer).</summary>
    void ShowSettings();

    /// <summary>Settles a car haul (PORTPLAN 20) and opens its receipt.</summary>
    void SettleHaul();
}
