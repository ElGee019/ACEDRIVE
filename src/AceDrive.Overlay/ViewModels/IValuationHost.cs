namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The market-valuation flow's reporting surface on the coordinator: reveal
/// the showcase page on arrival, hide the panel for the drive, and hand the
/// player back to the control page when the entry closes. Page building and
/// policy stay with the flow and the presenter, never here.
/// </summary>
public interface IValuationHost
{
    /// <summary>Reveals the panel on the valuation page (Unity's arrival).</summary>
    void ShowValuation(object screen);

    /// <summary>Hides the panel so the companion can guide the drive.</summary>
    void HidePanel();

    /// <summary>Returns to the control page once the showcase closes.</summary>
    void ShowControl();
}
