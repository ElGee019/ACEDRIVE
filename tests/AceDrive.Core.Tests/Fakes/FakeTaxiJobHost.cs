using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The shell a taxi job reports to: records every page and settle, and answers
/// the one question the flow asks — the player's home address.
/// </summary>
internal sealed class FakeTaxiJobHost : ITaxiJobHost
{
    private readonly object _controlPage = new();

    public List<object> Shown { get; } = [];

    public List<object> Revealed { get; } = [];

    public List<TaxiJobState> Settled { get; } = [];

    public int Hides { get; private set; }

    public int Refreshes { get; private set; }

    public int ControlRefreshes { get; private set; }

    public AddressLocation? Home { get; set; }

    public void ShowPage(object screen) => Shown.Add(screen);

    public void RevealPage(object screen) => Revealed.Add(screen);

    public void HidePanel() => Hides++;

    public void RefreshJob() => Refreshes++;

    public void RefreshControlPage() => ControlRefreshes++;

    public object ControlPage() => _controlPage;

    public AddressLocation? HomeAddress() => Home;

    public void SettleRide(TaxiJobState job) => Settled.Add(job);
}
