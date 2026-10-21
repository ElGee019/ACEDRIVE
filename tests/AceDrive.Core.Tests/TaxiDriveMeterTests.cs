using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The fare meter's integration guard: only sane passenger-leg ticks
/// count, the speed is clamped and reset clears the ride.</summary>
public sealed class TaxiDriveMeterTests
{
    [Fact]
    public void Add_IntegratesSpeedOverTime()
    {
        var meter = new TaxiDriveMeter(400.0);
        Run(meter, 50f, seconds: 36.0);

        Assert.Equal(0.5, meter.DistanceKm, 6);
    }

    [Fact]
    public void Add_CountsReverseMotionAsDistance()
    {
        var meter = new TaxiDriveMeter(400.0);
        Run(meter, -50f, seconds: 36.0);

        Assert.Equal(0.5, meter.DistanceKm, 6);
    }

    [Fact]
    public void Add_ClampsAGlitchedSpeed()
    {
        var meter = new TaxiDriveMeter(100.0);
        Run(meter, 1000f, seconds: 3.6);

        // 100 km/h for 3.6 s = 0.1 km, not 1 km.
        Assert.Equal(0.1, meter.DistanceKm, 6);
    }

    [Fact]
    public void Add_IgnoresStalledOrInvalidTicks()
    {
        var meter = new TaxiDriveMeter(400.0);
        meter.Add(50f, 0.0);
        meter.Add(50f, 1.0);
        meter.Add(50f, -0.1);
        meter.Add(50f, double.NaN);
        meter.Add(float.NaN, 0.1);
        meter.Add(float.PositiveInfinity, 0.1);

        Assert.Equal(0.0, meter.DistanceKm);
    }

    [Fact]
    public void Reset_ClearsTheRide()
    {
        var meter = new TaxiDriveMeter(400.0);
        Run(meter, 50f, seconds: 36.0);
        meter.Reset();

        Assert.Equal(0.0, meter.DistanceKm);
    }

    [Fact]
    public void InvalidCap_UsesTheTaxiRuleDefault()
    {
        var meter = new TaxiDriveMeter(double.NaN);
        Run(meter, 1000f, seconds: 3.6);

        // The fallback clamp is 400 km/h: 0.4 km in 3.6 s.
        Assert.Equal(0.4, meter.DistanceKm, 6);
    }

    // WHY 0.1 s ticks: the meter deliberately ignores dt >= 1 s (the poll's
    // stalled-frame guard), so a long run is simulated the way the 10 Hz poll
    // feeds it.
    private static void Run(TaxiDriveMeter meter, float speedKmh, double seconds)
    {
        const double tick = 0.1;
        int ticks = (int)Math.Round(seconds / tick);
        for (int i = 0; i < ticks; i++)
        {
            meter.Add(speedKmh, tick);
        }
    }
}
