using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The shell a POI world reports to: records each board refresh and purchase.
/// The calendar week is no longer a host read (the day cycle owns it), so the
/// fixture builds that collaborator with the week it wants.
/// </summary>
internal sealed class FakePoiWorldHost : IPoiWorldHost
{
    public int Refreshes { get; private set; }

    public List<CarSpec> Bought { get; } = [];

    public void RefreshBoard() => Refreshes++;

    public void CarBought(CarSpec car) => Bought.Add(car);
}
