namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the coordinator the day cycle reports to: the week roll that
/// repaints the cached calendar and menu, the readout repaint the shift
/// countdown forces, the shift-ended page (the coordinator owns the panel
/// policy that reveals it) and that page's way back to the main page.
/// Nothing here answers a question about the calendar: the week is a value the
/// pages read from the owner.
/// </summary>
public interface IDayCycleHost
{
    /// <summary>
    /// The display week rolled: the cached pages repaint now instead of on the
    /// next tick.
    /// </summary>
    void DisplayWeekChanged(int week);

    /// <summary>The job or shift state moved: the readouts are stale.</summary>
    void ReadoutsChanged();

    /// <summary>Shows the page of a shift that just ended.</summary>
    void ShowShiftEnded(object page);

    /// <summary>
    /// The shift-ended page's CONTINUE lands on the main page (the menu): a
    /// shift that ran out while the player was elsewhere must not drop them on
    /// the jobs list, same as the Cento Miglia receipt.
    /// </summary>
    void GoToMainPage();
}
