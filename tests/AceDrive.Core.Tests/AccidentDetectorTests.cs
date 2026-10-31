using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The collision latch: EVO only publishes cumulative damage, so a
/// positive jump is the crash and the previous sample holds the impact speed.</summary>
public sealed class AccidentDetectorTests
{
    private static readonly DateTime T0 = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TheFirstSample_OnlyPrimesTheBaseline()
    {
        var detector = new AccidentDetector();

        Assert.Null(detector.Observe(42.0, 120.0, T0));
    }

    [Fact]
    public void ADamageJump_ReportsTheSpeedFromBeforeTheHit()
    {
        var detector = new AccidentDetector();
        detector.Observe(0.0, 200.0, T0);

        // The crash happened between the samples: the car is already stopped
        // when the damage arrives, so the pre-impact speed must be used.
        AccidentImpact? impact = detector.Observe(180.0, 0.0, T0.AddMilliseconds(100));

        Assert.NotNull(impact);
        Assert.Equal(200.0, impact.ImpactSpeedKmh, 6);
        Assert.Equal(180.0, impact.DamageDelta, 6);
    }

    [Fact]
    public void OneCrashThatSettlesAcrossTwoTicks_IsOneImpact()
    {
        var detector = new AccidentDetector();
        detector.Observe(0.0, 100.0, T0);
        Assert.NotNull(detector.Observe(50.0, 0.0, T0.AddMilliseconds(100)));

        // The counter climbs again while the contact settles — same crash.
        AccidentImpact? second = detector.Observe(70.0, 0.0, T0.AddMilliseconds(200));

        Assert.Null(second);
    }

    [Fact]
    public void ANewHitAfterTheCooldown_IsANewImpact()
    {
        var detector = new AccidentDetector();
        detector.Observe(0.0, 100.0, T0);
        detector.Observe(50.0, 0.0, T0.AddMilliseconds(100));

        detector.Observe(50.0, 80.0, T0.AddSeconds(2));
        AccidentImpact? impact = detector.Observe(60.0, 0.0, T0.AddSeconds(2.1));

        Assert.NotNull(impact);
        Assert.Equal(80.0, impact.ImpactSpeedKmh, 6);
    }

    [Fact]
    public void ARepairDrop_IsNotAnImpact()
    {
        var detector = new AccidentDetector();
        detector.Observe(0.0, 100.0, T0);
        detector.Observe(200.0, 0.0, T0.AddMilliseconds(100));

        // The mechanic (or a session reload) cleared the counter: no bill.
        Assert.Null(detector.Observe(0.0, 0.0, T0.AddSeconds(3)));
    }

    [Fact]
    public void FloatNoiseOnAnUntouchedCounter_IsNotAnImpact()
    {
        var detector = new AccidentDetector();
        detector.Observe(10.0, 100.0, T0);

        Assert.Null(detector.Observe(10.0 + (AccidentDetector.DamageEpsilon / 2.0), 100.0, T0.AddMilliseconds(100)));
    }

    [Fact]
    public void ATornSample_ReprimesInsteadOfBillingGarbage()
    {
        var detector = new AccidentDetector();
        detector.Observe(0.0, 100.0, T0);
        detector.Observe(50.0, 0.0, T0.AddMilliseconds(100));

        Assert.Null(detector.Observe(double.NaN, double.NaN, T0.AddSeconds(2)));
        // The next good sample is the new baseline, so the gap is not a crash.
        Assert.Null(detector.Observe(50.0, 0.0, T0.AddSeconds(2.1)));
        Assert.NotNull(detector.Observe(60.0, 0.0, T0.AddSeconds(2.2)));
    }
}
