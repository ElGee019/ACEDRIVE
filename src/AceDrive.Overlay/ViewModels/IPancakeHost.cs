namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The weekly pancake visit's reporting surface on the coordinator: reveal the
/// briefing and the receipt, hide the panel for the drive, and hand the player
/// back to the page they came from (the calendar behind BACK, the menu behind
/// the receipt). Page building and every decision stay with the flow, exactly
/// like <see cref="IWhereIsWillyHost"/> — the two weekly events share this shape
/// so neither grows a second navigation path.
/// </summary>
public interface IPancakeHost
{
    /// <summary>Reveals the panel on the briefing or the receipt page.</summary>
    void Present(object page);

    /// <summary>Hides the panel so the companion can guide the drive.</summary>
    void HidePanel();

    /// <summary>Returns to the calendar the Friday row was tapped on.</summary>
    void ShowCalendar();

    /// <summary>
    /// Shows the menu once the receipt closes: the weekly visit is an event, not
    /// a job, so the player lands on the main page.
    /// </summary>
    void ShowMenu();
}
