namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The missing-person run's reporting surface on the coordinator
/// (ToWhereIsWilly.md §3.3): reveal the quest page, hide the panel for a drive
/// and hand the player back to the main page when the receipt closes. Page
/// building and policy stay with the flow and the presenter, never here.
/// </summary>
public interface IWhereIsWillyHost
{
    /// <summary>Reveals the panel on the quest page (search or found).</summary>
    void PresentQuest(object page);

    /// <summary>Hides the panel so the companion can guide the drive.</summary>
    void HidePanel();

    /// <summary>
    /// Shows the menu page once the receipt closes: the weekly run is an event,
    /// not a job, so the player lands on the main page instead of the jobs page
    /// the search was started from.
    /// </summary>
    void ShowMenu();

    /// <summary>
    /// Returns to the calendar the quest page was opened from: BACK before the
    /// first shop tap declines the run, and the row it was started from is the
    /// page the player expects to see again.
    /// </summary>
    void ShowCalendar();
}
