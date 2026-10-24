using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The shell a settle reports to: records the receipt page and every shared save
/// the finish triggers, so a settle can be asserted without a whole session.
/// </summary>
internal sealed class FakeJobSettlementHost : IJobSettlementHost
{
    private readonly object _controlPage = new();
    private readonly object _deliveryBoardPage = new();

    public List<object> Receipts { get; } = [];
    public List<RivalsResult?> DriverSaves { get; } = [];

    /// <summary>What <see cref="RefreshStandings"/> answers; null = nothing known.</summary>
    public RivalsResult? Standings { get; set; }

    public int JobRefreshes { get; private set; }
    public int ShiftSaves { get; private set; }
    public int ClockSaves { get; private set; }
    public int GarageSaves { get; private set; }
    public int ProgressBanked { get; private set; }
    public int StandingsRefreshes { get; private set; }
    public int DeliveryBoardRefreshes { get; private set; }
    public int DeliveryRunsCleared { get; private set; }
    public int ControlPages { get; private set; }
    public int DeliveryBoardPages { get; private set; }

    public void ShowReceipt(object screen) => Receipts.Add(screen);

    public void RefreshJob() => JobRefreshes++;

    public void SaveDriver(RivalsResult? standings) => DriverSaves.Add(standings);

    public void SaveShift() => ShiftSaves++;

    public void SaveClock() => ClockSaves++;

    public void SaveGarage() => GarageSaves++;

    public void OnProgressBanked() => ProgressBanked++;

    public RivalsResult? RefreshStandings()
    {
        StandingsRefreshes++;
        return Standings;
    }

    public void GoToControlPage() => ControlPages++;

    public void RefreshDeliveryBoard() => DeliveryBoardRefreshes++;

    public void ClearDeliveryRun() => DeliveryRunsCleared++;

    public void GoToDeliveryBoard() => DeliveryBoardPages++;

    /// <summary>The control page the receipts' BACK hands back to.</summary>
    public object ControlPage => _controlPage;

    /// <summary>The board page CONTINUE lands on.</summary>
    public object DeliveryBoardPage => _deliveryBoardPage;
}
