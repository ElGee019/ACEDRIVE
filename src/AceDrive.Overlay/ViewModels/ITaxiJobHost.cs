using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the shell a running taxi job needs: put a page up, refresh the
/// job line, and settle the ride through the receipt host.
///
/// The flow owns the job, its request and its speed challenge; the shell owns
/// the panel and the saves, so those arrive here rather than being reached for.
/// The trip-start breakdown is not here: both job flows take the same
/// <see cref="TripBreakdowns"/> roll.
/// </summary>
public interface ITaxiJobHost
{
    /// <summary>Swaps the page without revealing the panel (the request page).</summary>
    void ShowPage(object screen);

    /// <summary>Reveals the panel on a job page (refusal, breakdown, receipt).</summary>
    void RevealPage(object screen);

    /// <summary>Hides the panel: the HUD drives the leg.</summary>
    void HidePanel();

    /// <summary>Re-renders the job line and fare after the phase moved.</summary>
    void RefreshJob();

    /// <summary>The control page's own minute-guarded refresh (shift button state).</summary>
    void RefreshControlPage();

    /// <summary>The control page, for the proposal's BACK.</summary>
    object ControlPage();

    /// <summary>The player's home address, or null before the profile exists.</summary>
    AddressLocation? HomeAddress();

    /// <summary>Prices and banks the finished ride, then shows its receipt.</summary>
    void SettleRide(TaxiJobState job);
}
