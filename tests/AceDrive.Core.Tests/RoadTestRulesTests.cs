using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The 0-60 target and the crossing instant. The target is the game's own
/// metric (60 km/h, the unit cars.json's Acceleration column uses); a miles
/// player reads the same target converted, never a faster one.
/// </summary>
public sealed class RoadTestRulesTests
{
    [Fact]
    public void DisplayTarget_IsSixtyInKilometres()
    {
        Assert.Equal(60, RoadTestRules.DisplayTarget(MileageUnit.Kilometres));
    }

    [Fact]
    public void DisplayTarget_ConvertsTheSameSixtyToMiles()
    {
        // 60 km/h = 37.28 mph; the readout rounds it the way the speedometer
        // does, so the task text never claims a target the clock ignores.
        Assert.Equal(37, RoadTestRules.DisplayTarget(MileageUnit.Miles));
    }

    [Theory]
    [InlineData(59.9, false)]
    [InlineData(60.0, true)]
    [InlineData(-60.0, true)]
    [InlineData(double.NaN, false)]
    public void ReachedTarget_ReadsTheLiveSpeedInEitherDirection(
        double speedKmh, bool reached)
    {
        Assert.Equal(reached, RoadTestRules.ReachedTarget(speedKmh));
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.9, true)]
    [InlineData(1.0, true)]
    [InlineData(-1.0, true)]
    [InlineData(1.1, false)]
    [InlineData(20.0, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void IsStandstill_AllowsTheJitterBandAroundAHalt(
        double speedKmh, bool halted)
    {
        // EVO reports a little creep around a standstill, so the halt check has
        // to read the same band the HUD's parked state does — and a torn or
        // missing reading must never count as a halt.
        Assert.Equal(halted, RoadTestRules.IsStandstill(speedKmh));
    }

    [Theory]
    [InlineData(99.9, false)]
    [InlineData(100.0, true)]
    [InlineData(101.0, true)]
    [InlineData(-100.0, true)]
    [InlineData(double.NaN, false)]
    public void ReachedBrakeStart_ReadsTheHundredKilometreGate(
        double speedKmh, bool reached)
    {
        Assert.Equal(reached, RoadTestRules.ReachedBrakeStart(speedKmh));
    }

    [Fact]
    public void DisplaySpeed_ConvertsTheBrakeGateToThePlayersUnit()
    {
        // 100 km/h is the gate the brake board measures; a miles player reads
        // the same speed the speedometer would show (62 mph).
        Assert.Equal(100, RoadTestRules.DisplaySpeed(
            RoadTestRules.BrakeStartSpeedKmh, MileageUnit.Kilometres));
        Assert.Equal(62, RoadTestRules.DisplaySpeed(
            RoadTestRules.BrakeStartSpeedKmh, MileageUnit.Miles));
    }

    [Fact]
    public void DrivesThePick_MatchesThePickAndItsFolder()
    {
        // The pick is the variant row; a preset-less announcement is the folder
        // the log reports when it could not read the spawn line's preset.
        var pick = new CarSpec("ks_bmw_m2", 2023, "BMW", "M2", 2)
        {
            Preset = "preset_m2_mperf_1",
        };

        Assert.True(RoadTestRules.DrivesThePick(
            new CarIdentity("ks_bmw_m2", "preset_m2_mperf_1"), pick));
        Assert.True(RoadTestRules.DrivesThePick(
            new CarIdentity("ks_bmw_m2", null), pick));
    }

    [Fact]
    public void DrivesThePick_AnotherCarIsNotThePick()
    {
        var pick = new CarSpec("ks_abarth_695_biposto", 2014, "Abarth", "695 Biposto", 1);

        Assert.False(RoadTestRules.DrivesThePick(
            new CarIdentity("ks_mazda_mx5_na", null), pick));
    }

    [Fact]
    public void DrivesThePick_UnknownCarIsNotAMismatch()
    {
        // The watcher catches the spawn a moment after a cold start; refusing
        // on silence would lock the road test out of every fresh session.
        var pick = new CarSpec("ks_abarth_695_biposto", 2014, "Abarth", "695 Biposto", 1);

        Assert.True(RoadTestRules.DrivesThePick(null, pick));
    }
}
