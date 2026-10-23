using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>One proposal draw = two map addresses + their persisted residents
/// + the pickup coin flip, all from the seeded stream so a whole sequence
/// replays (Unity TaxiJobGenerator steps 1-6).</summary>
public sealed class TaxiJobProposerTests
{
    // Two pools with disjoint names, so every draw's origin is readable.
    private static readonly NamePoolTable Table = new(
    [
        new NamePool("a", 1.0, ["Anna", "Aribert"], ["Abt", "Amsel"]),
        new NamePool("b", 1.0, ["Bert", "Berta"], ["Bach", "Baumann"]),
    ],
    ["alone", "together"]);

    [Fact]
    public void SameSeed_ReplaysWholeProposals()
    {
        var first = TaxiTestData.Proposer(table: Table, seed: 9);
        var second = TaxiTestData.Proposer(table: Table, seed: 9);
        foreach (int _ in Enumerable.Range(0, 12))
        {
            Assert.Equal(
                first.Next(passengerCapacity: 4),
                second.Next(passengerCapacity: 4));
        }
    }

    private static readonly string[] FirstNames = ["Anna", "Aribert", "Bert", "Berta"];
    private static readonly string[] Surnames = ["Abt", "Amsel", "Bach", "Baumann"];
    private static readonly string[] Suffixes = ["alone", "together"];

    [Fact]
    public void Next_PairsTwoResidents_AndFlipsTheLegs()
    {
        var proposer = TaxiTestData.Proposer(table: Table, seed: 4);
        var flips = new HashSet<bool>();
        foreach (int _ in Enumerable.Range(0, 60))
        {
            TaxiProposal proposal = proposer.Next(passengerCapacity: 4);
            // Both residents come from the table's parts (any pool), and the
            // passenger keeps its companion suffix for the identity line.
            Assert.Contains(proposal.Customer.FirstName, FirstNames);
            Assert.Contains(proposal.SecondResident.Surname, Surnames);
            Assert.Contains(proposal.Customer.Suffix, Suffixes);
            // Every job has two doors on different streets (Unity's grouping).
            Assert.NotNull(proposal.CustomerHome);
            Assert.NotNull(proposal.SecondResidence);
            Assert.NotEqual(
                proposal.CustomerHome!.Value.StreetName,
                proposal.SecondResidence!.Value.StreetName);
            flips.Add(proposal.HomeIsPickup);
        }

        // Unity rolls the coin per job (Random.value < 0.5): 60 draws must
        // have shown both orders, or the flip is stuck on one leg.
        Assert.Equal(2, flips.Count);
    }

    [Fact]
    public void Residents_AreThePersistedHomeownersOfTheDrawnDoors()
    {
        var store = new FakeResidentStore();
        var proposer = TaxiTestData.Proposer(
            table: Table, seed: 4, residents: store);

        foreach (int _ in Enumerable.Range(0, 40))
        {
            TaxiProposal proposal = proposer.Next(passengerCapacity: 4);
            Resident customer = store.Residents.Single(
                resident => resident.Address == proposal.CustomerHome);
            Resident second = store.Residents.Single(
                resident => resident.Address == proposal.SecondResidence);

            Assert.Equal(customer.Name, proposal.Customer);
            Assert.Equal(second.Name, proposal.SecondResident);
        }

        // One row per address, never one per proposal.
        Assert.Equal(
            store.Residents.Select(resident => resident.Address).Distinct().Count(),
            store.Residents.Count);
    }

    [Fact]
    public void Radius_FiltersBothDrawnDoors()
    {
        // 20 km loop, two 10 km streets, one house every km: a 5 km radius is
        // meaningful here (the Brands Hatch stand-in is shorter than the floor).
        var map = new AddressMap(
            "Germany", "Test", 20_000.0, 1_000.0,
            [
                new AddressStreet("a", "A", 0.0, 0.5),
                new AddressStreet("b", "B", 0.5, 1.0),
            ]);
        var home = new AddressLocation("A", 1, HouseSide.Right);
        var proposer = TaxiTestData.Proposer(map: map, table: Table, seed: 2);

        foreach (int _ in Enumerable.Range(0, 40))
        {
            TaxiProposal proposal = proposer.Next(
                passengerCapacity: 4, home, radiusKm: 5);
            Assert.True(AddressBook.DistanceMeters(
                map, home, proposal.CustomerHome!.Value) <= 5_000.0);
            Assert.True(AddressBook.DistanceMeters(
                map, home, proposal.SecondResidence!.Value) <= 5_000.0);
        }
    }

    [Fact]
    public void MapWithoutTwoStreets_StillDrawsNames_ButNoDoors()
    {
        var map = new AddressMap(
            "Germany", "Test", 1_929.0, 30.0,
            [new AddressStreet("only", "Only Street", 0.0, 1.0)]);
        var store = new FakeResidentStore();
        var proposer = TaxiTestData.Proposer(
            map: map, table: Table, seed: 1, residents: store);

        TaxiProposal proposal = proposer.Next(passengerCapacity: 1);

        Assert.Null(proposal.CustomerHome);
        Assert.Null(proposal.SecondResidence);
        Assert.False(string.IsNullOrWhiteSpace(proposal.Customer.FirstName));
        Assert.Empty(store.Residents);
    }

    [Fact]
    public void SingleSeatCar_DropsTheCompanionSuffix_OnTheRideOnly()
    {
        var store = new FakeResidentStore();
        var proposer = TaxiTestData.Proposer(
            table: Table, seed: 4, residents: store);

        TaxiProposal solo = proposer.Next(passengerCapacity: 1);
        Resident customer = store.Residents.Single(
            resident => resident.Address == solo.CustomerHome);

        // One seat = one customer: the ride never promises companions.
        Assert.Empty(solo.Customer.Suffix);
        Assert.Equal(solo.Customer.BaseName, solo.Customer.Full);
        // The resident keeps the full drawn identity, so the same door can
        // still carry a group when a bigger car draws it.
        Assert.NotEmpty(customer.Name.Suffix);
    }

    [Fact]
    public void TwoSeatCar_KeepsTheCompanionSuffix()
    {
        var proposer = TaxiTestData.Proposer(table: Table, seed: 4);

        TaxiProposal shared = proposer.Next(passengerCapacity: 2);

        Assert.Contains(shared.Customer.Suffix, Suffixes);
    }
}
