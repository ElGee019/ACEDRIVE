using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// Records the dropoffs a Touristenfahrten run settled and holds the receipt's
/// CONTINUE, so a test decides when the player pressed it.
/// </summary>
internal sealed class FakeTouristenSettlement : ITouristenSettlement
{
    public List<(TouristenDropoff Dropoff, double DistanceKm)> Deliveries { get; } = [];

    public int SettleCalls => Deliveries.Count;

    public Action? ContinueAction { get; private set; }

    public ITouristenfahrtenHost? Host { get; private set; }

    public void Settle(
        TouristenDropoff dropoff, double distanceKm,
        ITouristenfahrtenHost host, Action continueAction)
    {
        Deliveries.Add((dropoff, distanceKm));
        Host = host;
        ContinueAction = continueAction;
    }

    /// <summary>Presses CONTINUE on the receipt the settle just showed.</summary>
    public void ContinueReceipt() => ContinueAction?.Invoke();
}
