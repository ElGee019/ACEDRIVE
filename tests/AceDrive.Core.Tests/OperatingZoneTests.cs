using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The operating-zone growth (item 58): Unity's threshold curve (2 first,
/// 3 second, then a 4-6 lottery), one street per expansion, the counter
/// reset, the duplicate rejection and the home-street seed.
/// </summary>
public sealed class OperatingZoneTests
{
    [Fact]
    public void FreshDriver_SeedsTheHomeStreet()
    {
        // Unity registers the home street during the Grandma dialogue, so
        // the count the bank's interest reads is 1 from day one.
        var zone = new OperatingZone(null, seed: 0);
        Assert.Equal([OperatingZone.HomeStreetId], zone.StreetIds);
        Assert.Equal(0, zone.CountedJobs);
        Assert.Equal(0.0, zone.Km);
    }

    [Fact]
    public void InitialKm_SeedsAFreshZone_ButTheSavedLengthWins()
    {
        // The composition root seeds the placeholder-map length; a real
        // saved zone length is never overwritten by that interim.
        var fresh = new OperatingZone(null, seed: 0, initialKm: 1.929);
        Assert.Equal(1.929, fresh.Km, precision: 9);

        var saved = new DriverState(0.0, 0.0, ZoneKm: 3.5);
        var resumed = new OperatingZone(saved, seed: 0, initialKm: 1.929);
        Assert.Equal(3.5, resumed.Km, precision: 9);
    }

    [Fact]
    public void FirstExpansion_NeedsTwoJobs()
    {
        var zone = new OperatingZone(null, seed: 0);
        Assert.Equal(2, zone.JobsUntilNext);
        zone.RegisterJob();
        Assert.False(zone.ExpansionDue);
        zone.RegisterJob();
        Assert.True(zone.ExpansionDue);
        Assert.True(zone.TryExpand("s1", 0.5));
        Assert.Equal(0, zone.CountedJobs);
        Assert.Equal(2, zone.StreetIds.Count);
        Assert.Equal(0.5, zone.Km);
    }

    [Fact]
    public void SecondExpansion_NeedsThreeJobs()
    {
        var zone = new OperatingZone(null, seed: 0);
        zone.RegisterJob();
        zone.RegisterJob();
        Assert.True(zone.TryExpand("s1", 0.0));
        Assert.Equal(3, zone.JobsUntilNext);
        Assert.False(zone.ExpansionDue);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void LaterExpansions_NeedFourToSixJobs(int parked)
    {
        // Seed hunt: pins the curve family, not the exact dice — after two
        // expansions the next threshold must land in 4..6 inclusive.
        var zone = new OperatingZone(null, seed: parked);
        for (int e = 0; e < 2; e++)
        {
            while (!zone.ExpansionDue) zone.RegisterJob();
            Assert.True(zone.TryExpand($"s{e + 1}", 0.0));
        }
        Assert.InRange(zone.JobsUntilNext, 4, 6);
    }

    [Fact]
    public void NotDueExpand_ChangesNothing()
    {
        var zone = new OperatingZone(null, seed: 0);
        zone.RegisterJob();
        Assert.False(zone.TryExpand("s1", 1.0));
        Assert.Equal(1, zone.CountedJobs);
        Assert.Single(zone.StreetIds);
        Assert.Equal(0.0, zone.Km);
    }

    [Fact]
    public void DuplicateStreet_Rejected()
    {
        var zone = new OperatingZone(null, seed: 0);
        zone.RegisterJob();
        zone.RegisterJob();
        Assert.True(zone.TryExpand("s1", 0.0));
        zone.RegisterJob();
        zone.RegisterJob();
        zone.RegisterJob();
        // The home street can never register twice either.
        Assert.False(zone.TryExpand(OperatingZone.HomeStreetId, 1.0));
        Assert.NotEqual(1.0, zone.Km);
    }

    [Fact]
    public void Resume_RestoresCountersAndStreets_AndDoesNotReseed()
    {
        var saved = new DriverState(0, 0, StreetIds: ["home", "s1"], ExpansionJobs: 1,
            ExpansionCount: 1);
        var zone = new OperatingZone(saved, seed: 3);
        Assert.Equal(["home", "s1"], zone.StreetIds);
        Assert.Equal(1, zone.CountedJobs);
        // Second-expansion threshold (3) with one job already counted.
        Assert.Equal(2, zone.JobsUntilNext);
    }
}
