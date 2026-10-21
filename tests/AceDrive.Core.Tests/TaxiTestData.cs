using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// Deterministic taxi fixtures: one place wires a proposer to a fresh
/// resident book, so constructor changes never fan out through the tests.
/// </summary>
internal static class TaxiTestData
{
    public static TaxiJobProposer Proposer(
        AddressMap? map = null, NamePoolTable? table = null, int seed = 0,
        IResidentStore? residents = null) =>
        new(map ?? AddressMap.Default,
            new ResidentBook(
                residents ?? new FakeResidentStore(),
                new ProceduralNameGenerator(table ?? NamePoolTable.Default, seed),
                new NullLogger()),
            seed);
}
