namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The panel slice the road test needs: open its page at the test centre, take
/// the panel down so the player can drive, and hand it back when the player
/// leaves. The flow owns the phases and the clock; the coordinator owns the
/// panel policy (AGENTS.md: a screen never opens another screen).
/// </summary>
public interface IRoadTestHost
{
    /// <summary>Reveals a page without waiting for the player to ask again.</summary>
    void RevealPage(object page);

    /// <summary>Shows the road test's settled receipt page (the photo leg).</summary>
    void ShowReceipt(object screen);

    /// <summary>Takes the panel down; the companion keeps guiding.</summary>
    void HidePanel();

    /// <summary>Shows the control page again once the test is left.</summary>
    void ShowControl();

    /// <summary>
    /// Shows the menu page — the page a call opens — for the end of a completed
    /// test: the visit is over, so the player lands on the main page instead of
    /// the jobs page the receipt was opened over.
    /// </summary>
    void ShowMenu();
}
