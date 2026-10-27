using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// Deterministic taxi fixtures: one place wires a proposer to a fresh
/// resident book and to the operating zone the draw is scoped to, so
/// constructor changes never fan out through the tests.
/// </summary>
internal static class TaxiTestData
{
    public static TaxiJobProposer Proposer(
        AddressMap? map = null, NamePoolTable? table = null, int seed = 0,
        IResidentStore? residents = null, OperatingZone? zone = null) =>
        new(zone ?? FullZone(map ?? AddressMap.Default),
            new ResidentBook(
                residents ?? new FakeResidentStore(),
                new ProceduralNameGenerator(table ?? NamePoolTable.Default, seed),
                new NullLogger()),
            seed);

    /// <summary>
    /// A zone that has discovered every street of <paramref name="map"/>, so a
    /// fixture map's draws behave as before the zone scoping. The zone reads
    /// only the street COUNT from a save, so the map's own ids are the honest
    /// fixture for it.
    /// </summary>
    public static OperatingZone FullZone(AddressMap map) =>
        new(
            DriverState.Fresh with
            {
                StreetIds = [.. map.Streets.Select(street => street.Id)],
            },
            seed: 0, map);
}
