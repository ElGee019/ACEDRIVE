// What a care-package run needs from the shell (PORTPLAN items 21/61): the
// panel policy, the pages a finished run opens (its receipt, then the reward
// picker) and the way back to the jobs page. The flow owns the run; the host
// owns the screen, so the flow never swaps pages itself.
namespace AceDrive.Overlay.ViewModels;

/// <summary>The shell slice a <see cref="CarePackageFlow"/> reaches.</summary>
public interface ICarePackageHost
{
    /// <summary>Puts the run's page up (the receipt, then the reward picker)
    /// and hides the run's HUD leg.</summary>
    void PresentPage(object page);

    /// <summary>Hides the panel so the companion can guide the drive.</summary>
    void HidePanel();

    /// <summary>Returns to the control page the run was started from.</summary>
    void ShowControl();
}
