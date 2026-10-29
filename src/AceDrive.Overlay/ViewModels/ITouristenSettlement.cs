using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The one thing a Touristenfahrten run does with a delivered order: price it,
/// bank it and put its receipt up. The flow only needs the call and the
/// receipt's CONTINUE back, so a fake settlement substitutes cleanly in tests
/// (LSP).
/// </summary>
public interface ITouristenSettlement
{
    /// <summary>
    /// Banks one delivered dropoff and shows its receipt. The receipt's
    /// CONTINUE runs <paramref name="continueAction"/>.
    /// </summary>
    void Settle(
        TouristenDropoff dropoff, double distanceKm,
        ITouristenfahrtenHost host, Action continueAction);
}
