using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The speed-challenge patrol draw (PORTPLAN item 11, Phase C):
/// unique doors, both stops kept clear, and offsets on the runtime lap.</summary>
public sealed class PolicePatrolPlannerTests
{
    private static readonly AddressLocation Pickup =
        new("Street 1", 5, HouseSide.Right);

    private static readonly AddressLocation Dropoff =
        new("Street 2", 20, HouseSide.Right);

    [Fact]
    public void Plan_SkipsBothStopsAndReturnsUniqueHouses()
    {
        var map = AddressMap.Default;
        var rules = new PoliceRules(
            PatrolsPerJob: 5, PatrolRadiusMeters: 30.0, SpeedLimitKmh: 50,
            ExcludedStopRadiusMeters: 60.0, WarningRadiusMeters: 120.0);
        IReadOnlyList<PolicePatrol> patrols =
            new PolicePatrolPlanner(map, rules, seed: 0)
                .Plan(Pickup, Dropoff, map.TrackLengthMeters, count: 20);

        Assert.Equal(20, patrols.Count);
        Assert.Equal(20, patrols.Select(patrol => patrol.Address).Distinct().Count());
        foreach (PolicePatrol patrol in patrols)
        {
            Assert.True(AddressBook.DistanceMeters(map, Pickup, patrol.Address) > 60.0);
            Assert.True(AddressBook.DistanceMeters(map, Dropoff, patrol.Address) > 60.0);
            Assert.Equal(
                AddressBook.LapOffsetMeters(map, patrol.Address, map.TrackLengthMeters),
                patrol.LapOffsetMeters, precision: 9);
            Assert.Equal(30.0, patrol.RadiusMeters);
            Assert.Equal(50, patrol.SpeedLimitKmh);
        }
        Assert.Equal(Enumerable.Range(1, 20), patrols.Select(patrol => patrol.Id));
    }

    [Fact]
    public void Plan_CountCapsAtTheAvailableHouses()
    {
        var map = AddressMap.Default;
        var planner = new PolicePatrolPlanner(map, PoliceRules.Default, seed: 0);

        Assert.Empty(planner.Plan(Pickup, Dropoff, map.TrackLengthMeters, count: 0));
        // 33 houses on each street at 1,929 m and 30 m spacing; even with a
        // zero keep-out the two exact stop doors stay out of the draw.
        var noClearance = new PolicePatrolPlanner(
            map, new PoliceRules(1, 30.0, 50, 0.0, 120.0), seed: 0);
        Assert.Equal(64, noClearance.Plan(
            Pickup, Dropoff, map.TrackLengthMeters, count: 1000).Count);

        // A keep-out wider than the map leaves no candidate door.
        var sealedOff = new PolicePatrolPlanner(
            map,
            new PoliceRules(1, 30.0, 50, 5000.0, 120.0),
            seed: 0);
        Assert.Empty(sealedOff.Plan(
            Pickup, Dropoff, map.TrackLengthMeters, count: 10));
    }

    [Fact]
    public void Plan_UsesTheRuntimeTrackLengthForOffsets()
    {
        var map = AddressMap.Default;
        var rules = new PoliceRules(3, 30.0, 60, 10.0, 100.0);
        const double runtimeLength = 4000.0;
        IReadOnlyList<PolicePatrol> patrols =
            new PolicePatrolPlanner(map, rules, seed: 3)
                .Plan(Pickup, Dropoff, runtimeLength, count: 3);

        Assert.Equal(3, patrols.Count);
        foreach (PolicePatrol patrol in patrols)
        {
            Assert.InRange(patrol.LapOffsetMeters, 0.0, runtimeLength);
            Assert.Equal(
                AddressBook.LapOffsetMeters(map, patrol.Address, runtimeLength),
                patrol.LapOffsetMeters, precision: 9);
            Assert.Equal(60, patrol.SpeedLimitKmh);
        }
    }

    [Fact]
    public void Plan_InvalidInput_ReturnsNoPatrols()
    {
        var map = AddressMap.Default;
        var planner = new PolicePatrolPlanner(map, PoliceRules.Default, seed: 0);

        Assert.Empty(planner.Plan(Pickup, Dropoff, double.NaN, count: 1));
        Assert.Empty(planner.Plan(Pickup, Dropoff, 0.0, count: 1));
        Assert.Empty(planner.Plan(Pickup, Dropoff, -1.0, count: 1));

        var emptyMap = new AddressMap("Nowhere", "Nowhere", 1929.0, 30.0, []);
        Assert.Empty(new PolicePatrolPlanner(emptyMap, PoliceRules.Default, seed: 0)
            .Plan(Pickup, Dropoff, 1929.0, count: 1));
    }
}
