using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The manual destination counter behind the ToDo tip (PORTPLAN 46).</summary>
public sealed class DestinationPinsTests
{
    [Fact]
    public void Fresh_StartsAtZero()
    {
        Assert.Equal(0, new DestinationPins().Count);
    }

    [Fact]
    public void Register_CountsEveryPinAndResumesFromTheDriver()
    {
        var pins = new DestinationPins(new DriverState(0.0, 0.0, ManuallySetDestinations: 5));

        Assert.Equal(5, pins.Count);
        Assert.Equal(6, pins.Register());
        Assert.Equal(6, pins.Count);
    }

    [Fact]
    public void Saved_NegativeCount_ClampsToZero()
    {
        Assert.Equal(0, new DestinationPins(
            new DriverState(0.0, 0.0, ManuallySetDestinations: -4)).Count);
    }
}
