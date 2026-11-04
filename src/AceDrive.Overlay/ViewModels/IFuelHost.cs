namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The fuel receipt's reporting surface on the coordinator: a fill is an event
/// the player did not ask the overlay for (EVO's pump moved the level while
/// they drove up to it), so the page interrupts whatever was up and hands it
/// back on CONTINUE, the way the insurance broker's page does. Page building
/// and pricing stay with the flow, never here.
/// </summary>
public interface IFuelHost
{
    /// <summary>Reveals the receipt over the current page, remembering it.</summary>
    void ShowFuelReceipt(object page);

    /// <summary>Hands back the page the receipt interrupted.</summary>
    void ResumeAfterFuelReceipt();
}
