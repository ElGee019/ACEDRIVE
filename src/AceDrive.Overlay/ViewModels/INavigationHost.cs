namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the coordinator the navigator reports to: the entries whose
/// work is not a page — closing a shift that already timed out (the
/// coordinator owns the shift record and its summary page), re-reading the
/// readouts after a shift starts, the settings page (which belongs to the
/// story flow rather than to navigation), and a destination pinned from the
/// addresses page (the ToDo list counts it). The haul settle is the haul
/// flow's page, so it takes no report at all.
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

    /// <summary>
    /// The addresses page pinned a destination (Unity manuallySetDestinations):
    /// the coordinator counts it and syncs the ToDo list.
    /// </summary>
    void DestinationPinned();
}
