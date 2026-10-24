using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the shell a live delivery run needs: the panel (hide it while the
/// companion drives, put a job page up), the companion's stop line, the hub page
/// the board hands over to, and the settle that prices the finished run.
///
/// The flow owns the run, the stops and the board page; only the shell knows
/// which page is showing, where a stop's door lives and which saves the settle
/// rides on, so those arrive here instead of being reached for. A host member
/// answers nothing about the run: the flow asks, the shell reports.
/// </summary>
public interface IDeliveryHost
{
    /// <summary>Swaps the page without revealing the panel (the board's BACK).</summary>
    void ShowPage(object screen);

    /// <summary>Reveals the panel on a job page (the breakdown interruption).</summary>
    void RevealPage(object screen);

    /// <summary>Hides the panel: the HUD drives every leg of the run.</summary>
    void HidePanel();

    /// <summary>
    /// Pins the leg being driven on the companion. False when the stop has no
    /// door left (a hand-edited catalogue), so the run skips it instead of
    /// stalling on an unreachable stop.
    /// </summary>
    bool PinStop(DeliveryStop leg);

    /// <summary>Drops the companion's job line.</summary>
    void ClearGuidance();

    /// <summary>The control page, for the board's BACK.</summary>
    object ControlPage();

    /// <summary>Opens the hub purchase page (the board's OPEN NEW CAREER).</summary>
    void OpenHub();

    /// <summary>Prices and banks the finished run, then shows its receipt.</summary>
    void Settle(DeliveryRun run);
}
