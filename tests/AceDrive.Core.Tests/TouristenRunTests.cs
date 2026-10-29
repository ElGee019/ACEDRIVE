using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The run's index moves only after the goods are loaded and only once per
/// delivered dropoff, so a double ENTER can never skip a corner.
/// </summary>
public sealed class TouristenRunTests
{
    private static readonly TouristenPickup Kind = TouristenMenu.Default.Pickups[0];

    [Fact]
    public void Advance_BeforeTheLoad_StaysAtTheFirstCorner()
    {
        var run = new TouristenRun(Pickup(), Kind, [Stop("a", 2), Stop("b", 3)]);

        Assert.NotNull(run.Advance());
        Assert.Equal(0, run.Index);
        Assert.False(run.IsComplete);
        Assert.False(run.Loaded);
    }

    [Fact]
    public void LoadAndAdvance_DeliversEveryStopThenCompletes()
    {
        var run = new TouristenRun(Pickup(), Kind, [Stop("a", 2), Stop("b", 3)]);

        run.Load();
        Assert.Equal("a", run.Current?.Door.Id);
        Assert.Equal("b", run.Advance()?.Door.Id);
        Assert.Equal("b", run.Current?.Door.Id);
        Assert.Null(run.Advance());
        Assert.True(run.IsComplete);
        Assert.Null(run.Advance());
    }

    private static PointOfInterest Pickup() => new(
        "test-cafe", "Test Café", new AddressLocation("Street 1", 1, HouseSide.Left),
        PoiTypes.CoffeeShop, "Owner", "coffee_beans");

    private static TouristenDropoff Stop(string id, int number) => new(
        new PointOfInterest(
            id, id, new AddressLocation("Street 1", number, HouseSide.Left),
            PoiTypes.TouristenDropoff),
        new TouristenOrder([]));
}
