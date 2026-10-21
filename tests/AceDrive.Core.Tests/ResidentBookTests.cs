using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>One homeowner per address, reused across proposals and reloads
/// (Unity TaxiJobResidentService).</summary>
public sealed class ResidentBookTests
{
    private static readonly NamePoolTable Table = new(
    [
        new NamePool("p", 1.0,
            ["Anna", "Bert", "Cara", "Dirk"],
            ["Abt", "Bach", "Cole", "Dorn"]),
    ],
    ["alone", "together"]);

    private static readonly AddressLocation Door =
        new("Street 1", 4, HouseSide.Left);

    [Fact]
    public void FirstVisit_DrawsAndBanksOneResident()
    {
        var store = new FakeResidentStore();
        var book = new ResidentBook(
            store, new ProceduralNameGenerator(Table, seed: 3), new NullLogger());

        Resident resident = book.GetOrCreate(Door);

        Assert.Single(store.Residents);
        Assert.Equal(1, store.Saves);
        Assert.False(string.IsNullOrWhiteSpace(resident.Name.FirstName));
        Assert.False(string.IsNullOrWhiteSpace(resident.Name.Surname));
    }

    [Fact]
    public void RepeatVisit_ReturnsTheSameHomeowner()
    {
        var store = new FakeResidentStore();
        var book = new ResidentBook(
            store, new ProceduralNameGenerator(Table, seed: 3), new NullLogger());

        Resident first = book.GetOrCreate(Door);
        Resident second = book.GetOrCreate(Door);

        Assert.Equal(first, second);
        Assert.Equal(1, store.Saves);
        Assert.Single(store.Residents);
    }

    [Fact]
    public void StreetName_MatchesIgnoringCase()
    {
        var store = new FakeResidentStore();
        var book = new ResidentBook(
            store, new ProceduralNameGenerator(Table, seed: 3), new NullLogger());

        Resident first = book.GetOrCreate(Door);
        Resident second = book.GetOrCreate(Door with { StreetName = "street 1" });

        Assert.Equal(first, second);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public void Reload_ReopensTheBankedResident()
    {
        var store = new FakeResidentStore();
        Resident first = new ResidentBook(
            store, new ProceduralNameGenerator(Table, seed: 3), new NullLogger())
            .GetOrCreate(Door);

        Resident reloaded = new ResidentBook(
            store, new ProceduralNameGenerator(Table, seed: 99), new NullLogger())
            .GetOrCreate(Door);

        Assert.Equal(first, reloaded);
        Assert.Equal(1, store.Saves);
    }

    [Fact]
    public void InvalidSavedRows_AreIgnored()
    {
        var store = new FakeResidentStore(
        [
            new Resident(new AddressLocation("Street 1", 1, HouseSide.Right),
                new ProceduralName("", "", "")),
            new Resident(new AddressLocation("", 2, HouseSide.Left),
                new ProceduralName("Anna", "Abt", "alone")),
        ]);
        var book = new ResidentBook(
            store, new ProceduralNameGenerator(Table, seed: 3), new NullLogger());

        Resident drawn = book.GetOrCreate(new AddressLocation(
            "Street 1", 1, HouseSide.Right));

        Assert.False(string.IsNullOrWhiteSpace(drawn.Name.FirstName));
        Assert.Equal(1, store.Saves); // the reloaded book saved its new draw
        Assert.Single(store.Residents);
    }

    [Fact]
    public void DrawName_DoesNotPersistWithoutAnAddress()
    {
        var store = new FakeResidentStore();
        var book = new ResidentBook(
            store, new ProceduralNameGenerator(Table, seed: 3), new NullLogger());

        ProceduralName name = book.DrawName();

        Assert.False(string.IsNullOrWhiteSpace(name.FirstName));
        Assert.Empty(store.Residents);
        Assert.Equal(0, store.Saves);
    }
}
