using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The slice of the shell a live Cento Miglia needs: hide the panel while the
/// companion drives, pin each leg, show the registration page and the receipt,
/// paint the live reputation and settle the finish.
///
/// The flow owns the race state machine and the registration check; the shell
/// owns the panel policy, the HUD, the saves and the settle, so those arrive here
/// instead of being reached for. A host member answers nothing about the race:
/// the flow asks, the shell reports.
/// </summary>
public interface ICentoRaceHost
{
    /// <summary>Swaps the page without revealing the panel (the abandoned entry's landing).</summary>
    void ShowPage(object screen);

    /// <summary>Reveals the panel on a page the entry owns (the registration page).</summary>
    void RevealPage(object screen);

    /// <summary>Hides the panel: the companion drives the leg.</summary>
    void HidePanel();

    /// <summary>The control page, the landing after an abandoned entry.</summary>
    object ControlPage();

    /// <summary>Pins the start door on the companion.</summary>
    void PinStart(CentoMigliaEvent race);

    /// <summary>Pins the finish door and starts the reputation counter.</summary>
    void PinFinish(CentoMigliaEvent race);

    /// <summary>Drops the companion's leg line and the reputation counter.</summary>
    void ClearGuidance();

    /// <summary>Paints the live club reputation on the companion, or clears it.</summary>
    void SetReputation(double? reputation);

    /// <summary>True while the car is parked on the pinned door.</summary>
    bool ParkedAtTarget { get; }

    /// <summary>Prices, banks and shows the finished race's receipt.</summary>
    void SettleRace(CentoMigliaEvent race, double startReputation, double endReputation);
}
