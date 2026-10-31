namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The market-valuation flow's reporting surface on the coordinator: reveal the
/// week's briefing, hide the panel for the drive, and hand the player back to
/// the control page once the entry closes or to the calendar the briefing came
/// from. The showcase page reaches the panel through the venue's POI handler,
/// and every page is built by the flow, never here.
/// </summary>
public interface IValuationHost
{
    /// <summary>Reveals the panel on the week's briefing page.</summary>
    void PresentBriefing(object page);

    /// <summary>Hides the panel so the companion can guide the drive.</summary>
    void HidePanel();

    /// <summary>Returns to the control page once the showcase closes.</summary>
    void ShowControl();

    /// <summary>Returns to the calendar the briefing was opened from.</summary>
    void ShowCalendar();
}
