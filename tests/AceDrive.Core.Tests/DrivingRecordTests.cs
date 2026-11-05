using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The career driving record (PORTPLAN 44): one bank per finished
/// job, the clean run, the longest run, the crash count and the
/// attentive-driver percentage derived from them.</summary>
public sealed class DrivingRecordTests
{
    [Fact]
    public void Bank_AddsTheJobAndPaysOneSkillPerFiveCleanKilometres()
    {
        var record = new DrivingRecord();

        Assert.Equal(1, record.Bank(8.2));
        Assert.Equal(8.2, record.JobDistanceKm, precision: 9);
        Assert.Equal(8.2, record.AccidentFreeKm, precision: 9);
        Assert.Equal(8.2, record.LongestAccidentFreeKm, precision: 9);

        // The threshold is shared across jobs: 8.2 + 1.8 reaches ten and pays
        // the second point, not a first point for the second job alone.
        Assert.Equal(1, record.Bank(1.8));
        Assert.Equal(10.0, record.JobDistanceKm, precision: 9);
    }

    [Fact]
    public void Bank_CarriesTheRunAcrossJobs()
    {
        var record = new DrivingRecord();

        Assert.Equal(0, record.Bank(4.9));
        Assert.Equal(1, record.Bank(0.1));
        Assert.Equal(5.0, record.AccidentFreeKm, precision: 9);
    }

    [Fact]
    public void Bank_IgnoresAStalledOrTornReading()
    {
        var record = new DrivingRecord();

        Assert.Equal(0, record.Bank(0.0));
        Assert.Equal(0, record.Bank(-3.0));
        Assert.Equal(0, record.Bank(double.NaN));
        Assert.Equal(0.0, record.JobDistanceKm, precision: 9);
    }

    [Fact]
    public void RegisterAccident_ResetsTheRunButKeepsTheRecordAndCountsTheCrash()
    {
        var record = new DrivingRecord();
        record.Bank(12.0);

        record.RegisterAccident();

        Assert.Equal(0.0, record.AccidentFreeKm, precision: 9);
        Assert.Equal(12.0, record.LongestAccidentFreeKm, precision: 9);
        Assert.Equal(12.0, record.JobDistanceKm, precision: 9);
        Assert.Equal(1, record.Accidents);

        // The new run climbs from zero and never shortens the record.
        Assert.Equal(1, record.Bank(5.0));
        Assert.Equal(5.0, record.AccidentFreeKm, precision: 9);
        Assert.Equal(12.0, record.LongestAccidentFreeKm, precision: 9);
    }

    [Fact]
    public void AttentiveRating_IsFullWhileNoCrashWasBilled()
    {
        var record = new DrivingRecord();

        Assert.Equal(100.0, record.AttentiveRating, precision: 9);
        record.Bank(8.2);
        Assert.Equal(100.0, record.AttentiveRating, precision: 9);
    }

    [Fact]
    public void AttentiveRating_IsTheCareerKmPerCrashCappedAtOneHundred()
    {
        var record = new DrivingRecord();
        record.Bank(40.0);
        record.RegisterAccident();
        Assert.Equal(40.0, record.AttentiveRating, precision: 9);

        // A hundred clean kilometres per crash is full marks; anything above
        // stays at the page's 100 % ceiling.
        record.Bank(160.0);
        Assert.Equal(100.0, record.AttentiveRating, precision: 9);
    }

    [Fact]
    public void AttentiveRating_IsZeroWhenACrashPrecedesTheFirstJobBank()
    {
        var record = new DrivingRecord();
        record.RegisterAccident();

        Assert.Equal(0.0, record.AttentiveRating, precision: 9);
        Assert.Equal(1, record.Accidents);
    }

    [Fact]
    public void Constructor_ClampsBrokenReadings()
    {
        var record = new DrivingRecord(
            jobDistanceKm: double.NaN,
            accidentFreeKm: -4.0,
            longestAccidentFreeKm: 3.0,
            accidents: -2);

        Assert.Equal(0.0, record.JobDistanceKm, precision: 9);
        Assert.Equal(0.0, record.AccidentFreeKm, precision: 9);
        Assert.Equal(3.0, record.LongestAccidentFreeKm, precision: 9);
        Assert.Equal(0, record.Accidents);

        // A record can never be shorter than the run it just loaded.
        var live = new DrivingRecord(accidentFreeKm: 4.0, longestAccidentFreeKm: 1.0);
        Assert.Equal(4.0, live.LongestAccidentFreeKm, precision: 9);
    }
}
