namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The market-valuation flow's reporting surface on the coordinator: hide the
/// panel for the drive and hand the player back to the control page when the
/// entry closes. The showcase page reaches the panel through the venue's POI
/// handler, and every page is built by the flow, never here.
/// </summary>
public interface IValuationHost
{
    /// <summary>Hides the panel so the companion can guide the drive.</summary>
    void HidePanel();

    /// <summary>Returns to the control page once the showcase closes.</summary>
    void ShowControl();
}
