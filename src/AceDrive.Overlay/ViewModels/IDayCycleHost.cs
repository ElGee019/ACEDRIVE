namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the coordinator the day cycle reports to: the week roll that
/// repaints the cached calendar and menu, the readout repaint the shift
/// countdown forces, and the shift-ended page (the coordinator owns the panel
/// policy that reveals it) with the way back to the screen the timeout
/// interrupted. Nothing here answers a question about the calendar: the week is
/// a value the pages read from the owner.
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
    /// The shift-ended page's CONTINUE: hands the player back to the page and
    /// visibility the timeout interrupted. A shift that ran out is a report,
    /// not a job they started, so it must not cost them their place — and a
    /// timeout that caught them driving stays on the road.
    /// </summary>
    void ResumeAfterShiftEnded();
}
