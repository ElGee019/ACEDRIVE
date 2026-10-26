using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia reputation ticker (PORTPLAN 49): the race grows the
/// club's standing with the kilometres the car actually covers, so a still car,
/// a paused game or a stalled poll never buys any.</summary>
public sealed class CentoReputationTickerTests
{
    [Fact]
    public void Add_StandingStillNeverGrowsTheReputation()
    {
        var ticker = new CentoReputationTicker(1.25, 12, 1);

        for (int i = 0; i < 600; i++)
        {
            ticker.Add(0f, 0.1);
        }

        Assert.Equal(0.0, ticker.DistanceKm);
        Assert.Equal(1.25, ticker.Value, precision: 12);
    }

    [Fact]
    public void Add_BanksTheDistanceTheCarCovers()
    {
        var ticker = new CentoReputationTicker(1.25, 12, 1);

        // Half a second at 100 km/h is 1/72 km.
        ticker.Add(100f, 0.5);

        Assert.Equal(100.0 / 3600.0 * 0.5, ticker.DistanceKm, precision: 12);
        Assert.Equal(
            CentoReputation.AfterDistance(1.25, 12, 1, ticker.DistanceKm),
            ticker.Value,
            precision: 12);
        Assert.True(ticker.Value > 1.25);
    }

    [Fact]
    public void Add_IgnoresAStalledTornOrReversedPoll()
    {
        var ticker = new CentoReputationTicker(1.0, 12, 1);

        ticker.Add(100f, 0.0);
        ticker.Add(100f, 1.0);       // a stalled poll: the odometer's guard
        ticker.Add(100f, -0.5);
        ticker.Add(float.NaN, 0.5);
        ticker.Add(100f, double.NaN);

        Assert.Equal(0.0, ticker.DistanceKm);
        Assert.Equal(1.0, ticker.Value, precision: 12);
    }

    [Fact]
    public void Add_ClampsATelemetryGlitch()
    {
        var glitched = new CentoReputationTicker(1.0, 12, 1);
        var fastest = new CentoReputationTicker(1.0, 12, 1);

        glitched.Add(5000f, 0.5);
        fastest.Add(500f, 0.5);

        Assert.Equal(fastest.DistanceKm, glitched.DistanceKm, precision: 12);
    }
}
