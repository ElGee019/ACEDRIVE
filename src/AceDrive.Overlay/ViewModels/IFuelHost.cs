namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The fuel events' reporting surface on the coordinator: a fill is an event
/// the player did not ask the overlay for (EVO's pump moved the level while
/// they drove up to it), and so is a dry tank (Unity's live run-dry halt). Both
/// pages interrupt whatever was up and hand it back on their one button, the
/// way the insurance broker's page does. Page building and pricing stay with
/// the flow, never here; the job the dry tank interrupted belongs to the
/// coordinator, which is why the failure is a host verb.
/// </summary>
public interface IFuelHost
{
    /// <summary>Reveals the receipt over the current page, remembering it.</summary>
    void ShowFuelReceipt(object page);

    /// <summary>Hands back the page the receipt interrupted.</summary>
    void ResumeAfterFuelReceipt();

    /// <summary>Reveals the repair page over the current page, remembering it.</summary>
    void ShowFuelOut(object page);

    /// <summary>Fails the taxi ride or delivery the dry tank interrupted and
    /// reports a Delivery Insurance refund (Unity's FailJobOnFuelOut).</summary>
    void FailJobForFuelOut();

    /// <summary>Hands back the page the repair page interrupted.</summary>
    void ResumeAfterFuelOut();
}
