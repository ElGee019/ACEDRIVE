using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The operating-zone growth (item 58): Unity's threshold curve (2 first,
/// 3 second, then a 4-6 lottery), one street per expansion, the counter
/// reset, the duplicate rejection, the home-street seed, and the addressable
/// area the zone exposes to every in-zone draw (<c>ZoneMap</c> + <c>NextStreet</c>).
/// </summary>
public sealed class OperatingZoneTests
{
    // Four rows so an expansion still has a next street to name: the base
    // pair the placeholder map ships plus two fictional rows.
    private static readonly AddressMap Pool = new(
        "Germany", "Brands", 1929.0, 30.0,
        [
            new AddressStreet("street-1", "Street 1", 0.0, 0.5),
            new AddressStreet("street-2", "Street 2", 0.5, 1.0),
            new AddressStreet("street-3", "Street 3", 0.0, 0.5, Fictional: true),
            new AddressStreet("street-4", "Street 4", 0.5, 1.0, Fictional: true),
        ]);

    private static string[] Names(AddressMap map) =>
        map.Streets.Select(street => street.Name).ToArray();

    [Fact]
    public void FreshDriver_SeedsTheHomeStreet()
    {
        // Unity registers the home street during the Grandma dialogue, so
        // the count the bank's interest reads is 1 from day one.
        var zone = new OperatingZone(null, seed: 0, AddressMap.Default);
        Assert.Equal([OperatingZone.HomeStreetId], zone.StreetIds);
        Assert.Equal(0, zone.CountedJobs);
        Assert.Equal(0.0, zone.Km);
        Assert.Equal(["Street 1"], Names(zone.ZoneMap));
    }

    [Fact]
    public void InitialKm_SeedsAFreshZone_ButTheSavedLengthWins()
    {
        // The composition root seeds the placeholder-map length; a real
        // saved zone length is never overwritten by that interim.
        var fresh = new OperatingZone(
            null, seed: 0, AddressMap.Default, initialKm: 1.929);
        Assert.Equal(1.929, fresh.Km, precision: 9);

        var saved = new DriverState(0.0, 0.0, ZoneKm: 3.5);
        var resumed = new OperatingZone(
            saved, seed: 0, AddressMap.Default, initialKm: 1.929);
        Assert.Equal(3.5, resumed.Km, precision: 9);
    }

    [Fact]
    public void HomeStreet_LeadsTheZone_AndTheNextStreetIsTheFirstMapRowLeft()
    {
        // A player whose home is Street 2 discovers it first; the first
        // expansion then claims Street 1, never the fictional pool first.
        var zone = new OperatingZone(
            null, seed: 0, Pool, homeStreetId: "street-2");
        Assert.Equal(["Street 2"], Names(zone.ZoneMap));
        Assert.Equal("street-1", zone.NextStreet?.Id);

        zone.RegisterJob();
        zone.RegisterJob();
        Assert.True(zone.TryExpand(zone.NextStreet!.Id, 0.5));

        Assert.Equal(["Street 2", "Street 1"], Names(zone.ZoneMap));
        Assert.Equal("street-3", zone.NextStreet?.Id);
    }

    [Fact]
    public void ZoneMap_IsCached_AndRebuiltWhenTheZoneGrows()
    {
        // Cache contract (AGENTS.md › Caching): the owner caches, the same
        // read answers the same instance, and the only writer rebuilds it.
        var zone = new OperatingZone(null, seed: 0, Pool);
        AddressMap before = zone.ZoneMap;
        Assert.Same(before, zone.ZoneMap);

        zone.RegisterJob();
        zone.RegisterJob();
        Assert.True(zone.TryExpand("street-2", 0.5));

        Assert.NotSame(before, zone.ZoneMap);
        Assert.Equal(["Street 1", "Street 2"], Names(zone.ZoneMap));
        Assert.Equal(2, zone.ZoneMap.Streets.Count);
    }

    [Fact]
    public void ZoneMap_IsCappedByTheMapAndThePoolEndsThere()
    {
        // A save may carry more ids than the data file has rows (an old zone,
        // a shrunk map): the filter never invents a street and NextStreet
        // answers null instead.
        var saved = new DriverState(
            0, 0, StreetIds: ["home", "s1", "s2", "s3", "s4", "s5"]);
        var zone = new OperatingZone(saved, seed: 0, Pool);

        Assert.Equal(4, zone.ZoneMap.Streets.Count);
        Assert.Null(zone.NextStreet);
    }

    [Fact]
    public void FirstExpansion_NeedsTwoJobs()
    {
        var zone = new OperatingZone(null, seed: 0, AddressMap.Default);
        Assert.Equal(2, zone.JobsUntilNext);
        zone.RegisterJob();
        Assert.False(zone.ExpansionDue);
        zone.RegisterJob();
        Assert.True(zone.ExpansionDue);
        Assert.True(zone.TryExpand("street-2", 0.5));
        Assert.Equal(0, zone.CountedJobs);
        Assert.Equal(["home", "street-2"], zone.StreetIds);
        Assert.Equal(["Street 1", "Street 2"], Names(zone.ZoneMap));
        Assert.Equal(0.5, zone.Km);
    }

    [Fact]
    public void SecondExpansion_NeedsThreeJobs()
    {
        var zone = new OperatingZone(null, seed: 0, AddressMap.Default);
        zone.RegisterJob();
        zone.RegisterJob();
        Assert.True(zone.TryExpand("street-2", 0.0));
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
        var zone = new OperatingZone(null, seed: parked, Pool);
        for (int e = 0; e < 2; e++)
        {
            while (!zone.ExpansionDue) zone.RegisterJob();
            Assert.True(zone.TryExpand(zone.NextStreet!.Id, 0.0));
        }
        Assert.InRange(zone.JobsUntilNext, 4, 6);
    }

    [Fact]
    public void NotDueExpand_ChangesNothing()
    {
        var zone = new OperatingZone(null, seed: 0, AddressMap.Default);
        zone.RegisterJob();
        Assert.False(zone.TryExpand("street-2", 1.0));
        Assert.Equal(1, zone.CountedJobs);
        Assert.Single(zone.StreetIds);
        Assert.Equal(0.0, zone.Km);
    }

    [Fact]
    public void DuplicateStreet_Rejected()
    {
        var zone = new OperatingZone(null, seed: 0, AddressMap.Default);
        zone.RegisterJob();
        zone.RegisterJob();
        Assert.True(zone.TryExpand("street-2", 0.0));
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
        var zone = new OperatingZone(saved, seed: 3, Pool);
        Assert.Equal(["home", "s1"], zone.StreetIds);
        Assert.Equal(1, zone.CountedJobs);
        // Legacy ids are only a count: the map names the two discovered
        // streets and the third row is what the next expansion claims.
        Assert.Equal(["Street 1", "Street 2"], Names(zone.ZoneMap));
        Assert.Equal("street-3", zone.NextStreet?.Id);
        // Second-expansion threshold (3) with one job already counted.
        Assert.Equal(2, zone.JobsUntilNext);
    }
}
