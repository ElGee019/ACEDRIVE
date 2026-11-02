using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The shell slice the car-haul job sees (PORTPLAN item 20): the page swap, the
/// companion's leg, and the notice line. The flow owns the offer and the run;
/// the shell owns the panel, the HUD and the save, so nothing here touches a
/// window or a file.
/// </summary>
public interface ICarHaulHost
{
    /// <summary>Swaps the visible page (the offer/job page).</summary>
    void ShowPage(object page);

    /// <summary>Puts the settled receipt up in the panel.</summary>
    void ShowReceipt(object page);


    /// <summary>Re-reads the cached control page after a phase change.</summary>
    void RefreshControl();

    /// <summary>Pins the leg the player must drive to, or clears it.</summary>
    void PinTarget(HudJobGuidance? guidance);

    /// <summary>One-off line under the banner (an unavailable offer, a settle note).</summary>
    void ShowNotice(string text);

    /// <summary>Names a refused tap on the control page's own haul button for a
    /// few seconds: the reason the player did not get a job, in the words the
    /// flow resolved.</summary>
    void ShowHaulGate(string text);
}
