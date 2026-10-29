using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The interpolated 0-60 crossing: the 10 Hz poll only brackets the instant, so
/// the reading lands between two samples instead of on a tenth of a second.
/// </summary>
public sealed class RoadTestTimingTests
{
    [Fact]
    public void Crossing_LandsBetweenTheTwoSamples()
    {
        // 56 km/h at 4.0 s, 62 km/h at 4.1 s: 60 is two thirds of the way, so
        // the run took 4.0 + 0.0667 s rather than a rounded 4.1 s.
        double seconds = RoadTestTiming.CrossingSeconds(
            previousSeconds: 4.0, previousSpeedKmh: 56.0,
            nowSeconds: 4.1, speedKmh: 62.0);

        Assert.Equal(4.0667, seconds, precision: 3);
    }

    [Fact]
    public void Crossing_ClampsToTheGreenLightWhenTheLaunchCameFirst()
    {
        // A player who pulled away during the counter crosses 60 before the
        // green light; the clock cannot measure a negative time.
        double seconds = RoadTestTiming.CrossingSeconds(
            previousSeconds: 0.1, previousSpeedKmh: 58.0,
            nowSeconds: 0.2, speedKmh: 70.0);

        Assert.True(seconds >= 0.0);
    }

    [Fact]
    public void Crossing_FallsBackToTheSampleWhenItDidNotCross()
    {
        // The previous sample was already above the target (a rolling start):
        // there is no crossing to interpolate, so the reading stays honest and
        // the flow leaves the leg running until a real 60 km/h crossing.
        Assert.Equal(3.0, RoadTestTiming.CrossingSeconds(
            previousSeconds: 2.9, previousSpeedKmh: 70.0,
            nowSeconds: 3.0, speedKmh: 80.0));
    }

    [Fact]
    public void Crossing_ToleratesADeceleratingCar()
    {
        Assert.Equal(3.0, RoadTestTiming.CrossingSeconds(
            previousSeconds: 2.9, previousSpeedKmh: 58.0,
            nowSeconds: 3.0, speedKmh: 55.0));
    }
}
