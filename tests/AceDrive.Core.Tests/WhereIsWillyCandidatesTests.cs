using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The missing-person draws (ToWhereIsWilly.md §2.2/§3.1): the town's coffee
/// shops, the residents a venue owner disqualifies, the deterministic pick and
/// the free-house fallback that keeps an empty resident table from dead-ending
/// the event.
/// </summary>
public sealed class WhereIsWillyCandidatesTests
{
    private static readonly string[] CoffeeIds = ["c1", "c2"];
    private static readonly string[] ResidentNames = ["Klaus Berger", "Otto Kern"];
    private static readonly string[] VenueOwners = ["ines voss", "Max Silvestri"];

    [Fact]
    public void CoffeeShops_KeepOnlyRealCoffeeDoors()
    {
        PointOfInterest[] doors =
        [
            Shop("c1", PoiTypes.CoffeeShop, 1),
            Shop("g1", PoiTypes.GroceryStore, 2),
            Shop("c2", PoiTypes.CoffeeShop, 3),
            // A coffee row without a resolvable house cannot be pinned.
            new("c3", "Broken", default, PoiTypes.CoffeeShop),
        ];

        IReadOnlyList<PointOfInterest> shops =
            WhereIsWillyCandidates.CoffeeShops(doors, AddressMap.Default);

        Assert.Equal(CoffeeIds, shops.Select(s => s.Id));
    }

    [Fact]
    public void CoffeeShops_DropDoorsOnUndiscoveredStreets()
    {
        // The zone has discovered Street 1 only: a shop on a fictional street
        // is not part of the search yet (TOZONES §4.7).
        AddressMap zoneMap = new(
            "Germany", "Brands", 1929.0, 30.0,
            [new AddressStreet("street-1", "Street 1", 0.0, 0.5)]);
        PointOfInterest[] doors =
        [
            Shop("c1", PoiTypes.CoffeeShop, 1),
            new("c2", "Off Zone", Door("Street 3", 4), PoiTypes.CoffeeShop),
        ];

        IReadOnlyList<PointOfInterest> shops =
            WhereIsWillyCandidates.CoffeeShops(doors, zoneMap);

        Assert.Equal(["c1"], shops.Select(s => s.Id));
    }

    [Fact]
    public void EligibleResidents_DropEveryVenueOwner()
    {
        Resident[] residents =
        [
            Resident("Klaus", "Berger"),
            Resident("Ines", "Voss"),
            Resident("Otto", "Kern"),
        ];

        IReadOnlyList<Resident> candidates = WhereIsWillyCandidates.EligibleResidents(
            residents, VenueOwners);

        Assert.DoesNotContain(candidates, r => r.Name.Surname == "Voss");
        Assert.Equal(ResidentNames, candidates.Select(r => r.Name.BaseName));
    }

    [Fact]
    public void EligibleResidents_EmptyInputAnswersEmpty()
    {
        Assert.Empty(WhereIsWillyCandidates.EligibleResidents([], VenueOwners));
        Assert.Empty(WhereIsWillyCandidates.EligibleResidents(null, null));
    }

    [Fact]
    public void Pick_IsDeterministicOnTheCallersSeededStream()
    {
        Resident[] residents =
        [
            Resident("Klaus", "Berger"),
            Resident("Ines", "Voss"),
            Resident("Otto", "Kern"),
        ];

        Resident? first = WhereIsWillyCandidates.Pick(residents, new Random(7).Next);
        Resident? again = WhereIsWillyCandidates.Pick(residents, new Random(7).Next);

        Assert.NotNull(first);
        Assert.Same(first, again);
        Assert.Null(WhereIsWillyCandidates.Pick([], new Random(7).Next));
    }

    [Fact]
    public void PickFreeHouse_SkipsEveryDoorAPoiOrResidentUses()
    {
        AddressMap map = AddressMap.Default;
        AddressStreet street = map.Streets[0];
        // Every number but the last one on the first street is taken, and the
        // whole second street with it: one door is left.
        List<AddressLocation> taken = [];
        int firstCount = HouseNumberPlan.Count(street, map);
        for (int number = 1; number < firstCount; number++)
        {
            taken.Add(Door(street.Name, number));
        }
        AddressStreet second = map.Streets[1];
        for (int number = 1; number <= HouseNumberPlan.Count(second, map); number++)
        {
            taken.Add(Door(second.Name, number));
        }

        AddressLocation? free = WhereIsWillyCandidates.PickFreeHouse(
            map, Generator(), taken);

        Assert.NotNull(free);
        Assert.Equal(firstCount, free.Value.HouseNumber);
        Assert.Equal(street.Name, free.Value.StreetName);
    }

    [Fact]
    public void PickFreeHouse_AnswersNullWhenEveryDoorIsTaken()
    {
        AddressMap map = AddressMap.Default;
        List<AddressLocation> taken = [];
        foreach (AddressStreet street in map.Streets)
        {
            for (int number = 1; number <= HouseNumberPlan.Count(street, map); number++)
            {
                taken.Add(Door(street.Name, number));
            }
        }

        Assert.Null(WhereIsWillyCandidates.PickFreeHouse(map, Generator(), taken));
    }

    [Fact]
    public void PickFreeHouse_NeverDrawsATakenDoor()
    {
        AddressMap map = AddressMap.Default;
        var taken = new List<AddressLocation>
        {
            Door(map.Streets[0].Name, 1),
            Door(map.Streets[0].Name, 2),
            Door(map.Streets[1].Name, 1),
        };

        AddressLocation? free = WhereIsWillyCandidates.PickFreeHouse(
            map, Generator(), taken);

        Assert.NotNull(free);
        Assert.DoesNotContain(free.Value, taken);
    }

    private static PoiGenerator Generator() =>
        new(AddressMap.Default, PoiCatalog.Default, seed: 0);

    private static PointOfInterest Shop(string id, string type, int number) =>
        new(id, "Shop " + id, Door("Street 1", number), type);

    private static AddressLocation Door(string street, int number) =>
        new(street, number, HouseNumberPlan.SideOf(number));

    private static Resident Resident(string first, string surname) =>
        new(Door("Street 2", 4), new ProceduralName(first, surname));
}
