using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The forced speed challenge's deadline (PORTPLAN 14): route distance and
/// the driven car's top speed, with the data-driven clamps and the five-second
/// rounding the HUD relies on.
/// </summary>
public sealed class SpeedChallengeTimerTests
{
    [Fact]
    public void Estimate_UsesTheCarTopSpeedAndRoundsUpToFiveSeconds()
    {
        // 1 km at 200 km/h: 110 km/h road pace, 32.7 s, slack 1.25, buffer 20.
        Assert.Equal(
            TimeSpan.FromSeconds(65),
            SpeedChallengeTimer.Estimate(
                1000.0, 200, SpeedChallengeRules.Default));
    }

    [Fact]
    public void Estimate_ClampsSlowAndFastCars()
    {
        // 100 km/h: 55 km/h road pace -> 65.5 s * 1.25 + 20 = 101.8 -> 105.
        Assert.Equal(
            TimeSpan.FromSeconds(105),
            SpeedChallengeTimer.Estimate(
                1000.0, 100, SpeedChallengeRules.Default));

        // 300 km/h: 130 km/h cap -> 27.7 s * 1.25 + 20 = 54.6 -> 55.
        Assert.Equal(
            TimeSpan.FromSeconds(55),
            SpeedChallengeTimer.Estimate(
                1000.0, 300, SpeedChallengeRules.Default));
    }

    [Fact]
    public void Estimate_MissingCarSpecUsesTheMinimumPace()
    {
        // 0 km/h falls back to the 40 km/h floor: 90 s * 1.25 + 20 = 132.5.
        Assert.Equal(
            TimeSpan.FromSeconds(135),
            SpeedChallengeTimer.Estimate(
                1000.0, topSpeedKmh: 0, SpeedChallengeRules.Default));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Estimate_UnusableRouteHasNoDeadline(double routeMeters)
    {
        Assert.Null(SpeedChallengeTimer.Estimate(
            routeMeters, 200, SpeedChallengeRules.Default));
    }

    [Fact]
    public void Estimate_InvalidRulesHaveNoDeadline()
    {
        var invalid = SpeedChallengeRules.Default with { SlackFactor = 0.0 };

        Assert.Null(SpeedChallengeTimer.Estimate(1000.0, 200, invalid));
    }

    [Fact]
    public void Estimate_LongerRouteGetsMoreTime()
    {
        TimeSpan? shortHop = SpeedChallengeTimer.Estimate(
            1000.0, 200, SpeedChallengeRules.Default);
        TimeSpan? longHop = SpeedChallengeTimer.Estimate(
            5000.0, 200, SpeedChallengeRules.Default);

        Assert.True(longHop > shortHop);
        Assert.Equal(TimeSpan.FromSeconds(225), longHop);
    }
}
