using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The shell a POI world reports to: records each board refresh and purchase,
/// and supplies the calendar week the dealer's floor is drawn from.
/// </summary>
internal sealed class FakePoiWorldHost : IPoiWorldHost
{
    public int Week { get; set; } = 1;

    public int Refreshes { get; private set; }

    public List<CarSpec> Bought { get; } = [];

    public int DisplayWeek => Week;

    public void RefreshBoard() => Refreshes++;

    public void CarBought(CarSpec car) => Bought.Add(car);
}
