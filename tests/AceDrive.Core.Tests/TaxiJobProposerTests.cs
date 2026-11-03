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
    public void MapWithoutHouses_StillDrawsNames_ButNoDoors()
    {
        // A map with no house number cannot place either leg, but the proposal
        // must still name the passenger and the second resident.
        var map = new AddressMap(
            "Germany", "Test", 1_929.0, 0.0,
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
    public void Draw_StaysInsideTheDiscoveredStreets()
    {
        // The zone has discovered Street 1 only; the map carries the whole
        // fictional pool. No proposal may send the player to a street the zone
        // has not reached (TOZONES §4.7).
        var map = new AddressMap(
            "Germany", "Test", 1_929.0, 30.0,
            [
                new AddressStreet("street-1", "Street 1", 0.0, 0.5),
                new AddressStreet("street-2", "Street 2", 0.5, 1.0),
                new AddressStreet("street-3", "Street 3", 0.0, 0.5, Fictional: true),
            ]);
        var zone = new OperatingZone(null, seed: 0, map);
        var proposer = TaxiTestData.Proposer(zone: zone, table: Table, seed: 5);

        foreach (int _ in Enumerable.Range(0, 40))
        {
            TaxiProposal proposal = proposer.Next(passengerCapacity: 4);
            AddressLocation home = proposal.CustomerHome!.Value;
            AddressLocation second = proposal.SecondResidence!.Value;

            Assert.Equal("Street 1", home.StreetName);
            Assert.Equal("Street 1", second.StreetName);
            Assert.NotEqual(home.HouseNumber, second.HouseNumber);
        }
    }

    [Fact]
    public void OneStreetZone_KeepsBothLegsOnThatStreet()
    {
        // One discovered street: Unity's two-street draw would fail here, so
        // the second residence is another house on the same street and the
        // proposal stays playable (TOZONES §4.7, §10).
        var map = new AddressMap(
            "Germany", "Test", 1_929.0, 30.0,
            [new AddressStreet("only", "Only Street", 0.0, 1.0)]);
        var proposer = TaxiTestData.Proposer(
            zone: TaxiTestData.FullZone(map), table: Table, seed: 3);

        foreach (int _ in Enumerable.Range(0, 25))
        {
            TaxiProposal proposal = proposer.Next(passengerCapacity: 4);
            AddressLocation home = proposal.CustomerHome!.Value;
            AddressLocation second = proposal.SecondResidence!.Value;

            Assert.Equal("Only Street", home.StreetName);
            Assert.Equal(home.StreetName, second.StreetName);
            Assert.NotEqual(home.HouseNumber, second.HouseNumber);
        }
    }

    [Fact]
    public void Next_LeavesThePlayersOwnDoorOut()
    {
        var home = new AddressLocation("Street 1", 9, HouseSide.Right);
        var proposer = TaxiTestData.Proposer(table: Table, seed: 7);

        foreach (int _ in Enumerable.Range(0, 40))
        {
            TaxiProposal proposal = proposer.Next(
                passengerCapacity: 4, home, radiusKm: 0);

            // The player already lives there: no customer can be sent home.
            Assert.False(proposal.CustomerHome!.Value.Matches(home));
            Assert.False(proposal.SecondResidence!.Value.Matches(home));
        }
    }

    [Fact]
    public void PoiDoor_IsDrawn_AndItsOwnerIsThePassenger()
    {
        // One street of three doors, the middle one a business: the draw must
        // reach it (no exclusion) and name its owner as the occupant instead
        // of banking a second citizen in the shop.
        var map = new AddressMap(
            "Germany", "Test", 60.0, 30.0,
            [new AddressStreet("only", "Only Street", 0.0, 1.0)]);
        var business = new AddressLocation("Only Street", 2, HouseSide.Left);
        var store = new FakeResidentStore();
        var directory = new PoiDirectory(
        [
            new PointOfInterest(
                "grocery", "Max's Grocery Store", business,
                PoiTypes.Grocery, "Giulia De Luca"),
        ]);
        var proposer = TaxiTestData.Proposer(
            map: map, table: Table, seed: 3, residents: store, pois: directory);

        bool sawTheShop = false;
        foreach (int _ in Enumerable.Range(0, 40))
        {
            TaxiProposal proposal = proposer.Next(passengerCapacity: 4);

            if (proposal.CustomerHome!.Value.Matches(business))
            {
                sawTheShop = true;
                Assert.Equal("Giulia", proposal.Customer.FirstName);
                Assert.Equal("De Luca", proposal.Customer.Surname);
            }

            if (proposal.SecondResidence!.Value.Matches(business))
            {
                sawTheShop = true;
                Assert.Equal("Giulia", proposal.SecondResident.FirstName);
                Assert.Equal("De Luca", proposal.SecondResident.Surname);
            }
        }

        Assert.True(sawTheShop);
        // One citizen per house: the owner is the shop's occupant, so the
        // resident book never gains a row at that door.
        Assert.NotEmpty(store.Residents);
        Assert.DoesNotContain(store.Residents, resident => resident.Address.Matches(business));
    }

    [Fact]
    public void PoiOwnerWithoutASurname_IsStillAWholePassenger()
    {
        // A fixed story friend owns their door with a single-token name; the
        // passenger must be that person, not a name plus an empty surname.
        var map = new AddressMap(
            "Germany", "Test", 60.0, 30.0,
            [new AddressStreet("only", "Only Street", 0.0, 1.0)]);
        var door = new AddressLocation("Only Street", 1, HouseSide.Right);
        var directory = new PoiDirectory(
        [
            new PointOfInterest("annabellas_place", "Annabella's Place", door,
                PoiTypes.Person, "Annabelle"),
        ]);
        var proposer = TaxiTestData.Proposer(
            map: map, table: Table, seed: 3, pois: directory);

        bool sawTheDoor = false;
        foreach (int _ in Enumerable.Range(0, 40))
        {
            TaxiProposal proposal = proposer.Next(passengerCapacity: 4);
            if (!proposal.CustomerHome!.Value.Matches(door)
                && !proposal.SecondResidence!.Value.Matches(door))
            {
                continue;
            }

            sawTheDoor = true;
            ProceduralName owner = proposal.CustomerHome!.Value.Matches(door)
                ? proposal.Customer
                : proposal.SecondResident;
            Assert.Equal("Annabelle", owner.Full);
            Assert.Equal("Annabelle", owner.FamilyName);
        }

        Assert.True(sawTheDoor);
    }

    [Fact]
    public void BusinessDoors_StayInThePool()
    {
        // Every door of the street belongs to a business: the draw must still
        // resolve both legs (an exclusion would have left no door at all).
        var map = new AddressMap(
            "Germany", "Test", 60.0, 30.0,
            [new AddressStreet("only", "Only Street", 0.0, 1.0)]);
        var directory = new PoiDirectory();
        int houses = HouseNumberPlan.Count(map.Streets[0], map);
        for (int number = 1; number <= houses; number++)
        {
            directory.Add(new PointOfInterest(
                $"place-{number}", $"Place {number}",
                new AddressLocation("Only Street", number, HouseNumberPlan.SideOf(number)),
                PoiTypes.Grocery, $"Owner {number}"));
        }

        var store = new FakeResidentStore();
        TaxiProposal proposal = TaxiTestData
            .Proposer(map: map, table: Table, seed: 5, residents: store, pois: directory)
            .Next(passengerCapacity: 4);

        Assert.NotNull(proposal.CustomerHome);
        Assert.NotNull(proposal.SecondResidence);
        // Both occupants are owners, so no resident is invented behind them.
        Assert.Empty(store.Residents);
        Assert.StartsWith("Owner ", proposal.Customer.BaseName);
        Assert.StartsWith("Owner ", proposal.SecondResident.BaseName);
    }

    [Fact]
    public void AStreetOfTwoHouses_KeepsTheSameTwoResidentsEveryDraw()
    {
        // One citizen per house, and the citizen persists: with only two doors
        // both are drawn every time, so the same pair of people must come back
        // instead of two fresh names per proposal.
        var map = new AddressMap(
            "Germany", "Test", 1_000.0, 600.0,
            [new AddressStreet("only", "Only Street", 0.0, 1.0)]);
        var store = new FakeResidentStore();
        var proposer = TaxiTestData.Proposer(
            map: map, table: Table, seed: 6, residents: store);

        TaxiProposal first = proposer.Next(passengerCapacity: 4);
        TaxiProposal second = proposer.Next(passengerCapacity: 4);

        Assert.Equal(2, store.Residents.Count);
        Assert.Equal(Doors(first), Doors(second));
        Assert.Equal(ResidentNames(first), ResidentNames(second));
    }

    private static string[] Doors(TaxiProposal proposal)
    {
        // The two legs may swap roles between draws; the pair is what matters.
        string[] doors =
        [
            proposal.CustomerHome!.Value.DoorKey,
            proposal.SecondResidence!.Value.DoorKey,
        ];
        Array.Sort(doors, StringComparer.Ordinal);
        return doors;
    }

    private static string[] ResidentNames(TaxiProposal proposal)
    {
        string[] names = [proposal.Customer.BaseName, proposal.SecondResident.BaseName];
        Array.Sort(names, StringComparer.Ordinal);
        return names;
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
