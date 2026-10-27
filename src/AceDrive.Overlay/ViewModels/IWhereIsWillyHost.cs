namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The missing-person run's reporting surface on the coordinator
/// (ToWhereIsWilly.md §3.3): reveal the quest page, hide the panel for a drive
/// and hand the player back to the control page when the receipt closes. Page
/// building and policy stay with the flow and the presenter, never here.
/// </summary>
public interface IWhereIsWillyHost
{
    /// <summary>Reveals the panel on the quest page (search or found).</summary>
    void PresentQuest(object page);

    /// <summary>Hides the panel so the companion can guide the drive.</summary>
    void HidePanel();

    /// <summary>Returns to the control page once the receipt closes.</summary>
    void ShowControl();
}
