using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The receipt's numbers (ToWhereIsWilly.md §2.5/§2.7): the exact fee, base and
/// multipliers for a first-try find, a three-attempt find, a one-shop town, an
/// empty garage and a premium garage. The page and the flow read these numbers
/// from one builder, so this suite is the contract both are held to.
/// </summary>
public sealed class WhereIsWillyReceiptBuilderTests
{
    private static readonly WhereIsWillyRules Rules = WhereIsWillyRules.Default;

    [Fact]
    public void FirstTryFind_OnATenShopTown_UsesTheThreeTimesBracket()
    {
        WhereIsWillyReceipt receipt = Build(
            attempts: 0, cars: [Car(30000.0)], shopCount: 10);

        // 30,000 * 0.04 = 1,200 base; x3.0 first try; x1.75 for ten shops.
        Assert.Equal(1200.0, receipt.BaseReward, 6);
        Assert.Equal(1.75, receipt.ShopMultiplier, 6);
        Assert.Equal(3.0, receipt.AttemptMultiplier, 6);
        Assert.Equal(6300, receipt.FinderFee);
        Assert.True(receipt.FirstTry);
    }

    [Fact]
    public void ThreeAttemptFind_DropsToTheHalfFloor()
    {
        WhereIsWillyReceipt receipt = Build(
            attempts: 3, cars: [Car(30000.0)], shopCount: 10);

        Assert.Equal(0.5, receipt.AttemptMultiplier, 6);
        Assert.Equal(1050, receipt.FinderFee);
        Assert.Equal(3, receipt.Attempts);
        Assert.False(receipt.FirstTry);
    }

    [Fact]
    public void OneShopTown_AlwaysMultipliesByOne()
    {
        WhereIsWillyReceipt receipt = Build(
            attempts: 0, cars: [Car(10000.0)], shopCount: 1);

        Assert.Equal(1.0, receipt.ShopMultiplier, 6);
        Assert.Equal(1.0, receipt.AttemptMultiplier, 6);
        Assert.Equal(400, receipt.FinderFee);
    }

    [Fact]
    public void EmptyGarage_PaysTheFloor()
    {
        WhereIsWillyReceipt receipt = Build(attempts: 0, cars: [], shopCount: 2);

        Assert.Equal(0.0, receipt.AverageCarValue, 6);
        Assert.Equal(0.0, receipt.BaseReward, 6);
        Assert.Equal(100, receipt.FinderFee);
    }

    [Fact]
    public void PremiumGarage_PaysTheMultipliedShare()
    {
        WhereIsWillyReceipt receipt = Build(
            attempts: 0, cars: [Car(100000.0), Car(200000.0)], shopCount: 4);

        // Average 150,000 * 0.04 = 6,000; four shops x1.25; first try x3.0.
        Assert.Equal(150000.0, receipt.AverageCarValue, 6);
        Assert.Equal(22500, receipt.FinderFee);
    }

    [Fact]
    public void Build_CarriesTheIdentityTheReceiptPrints()
    {
        Resident person = Person("Klaus", "Berger");
        PointOfInterest shop = Shop("Klaus's Coffee Shop", 7);

        WhereIsWillyReceipt receipt = WhereIsWillyReceiptBuilder.Build(
            Rules, person, shop, attempts: 1, ownedCars: [Car(20000.0)],
            shopCount: 3, week: 3, year: 1924, friendshipLevel: 2);

        Assert.Equal("Klaus Berger", receipt.PersonName);
        Assert.Equal("Klaus", receipt.FirstName);
        Assert.Equal("Klaus's Coffee Shop", receipt.ShopName);
        Assert.Equal(3, receipt.Week);
        Assert.Equal(1924, receipt.Year);
        Assert.Equal(2, receipt.FriendshipLevel);
    }

    [Fact]
    public void Build_IgnoresNegativeAttemptsAndCounts()
    {
        WhereIsWillyReceipt receipt = WhereIsWillyReceiptBuilder.Build(
            Rules, Person("Ines", "Voss"), Shop("Café", 1), attempts: -2,
            ownedCars: [Car(20000.0)], shopCount: -1, week: 1, year: 1924,
            friendshipLevel: -1);

        Assert.Equal(0, receipt.Attempts);
        Assert.Equal(0, receipt.ShopCount);
        Assert.Equal(0, receipt.FriendshipLevel);
        Assert.Equal(100, receipt.FinderFee);
    }

    private static WhereIsWillyReceipt Build(
        int attempts, IReadOnlyList<CarSpec?> cars, int shopCount) =>
        WhereIsWillyReceiptBuilder.Build(
            Rules, Person("Klaus", "Berger"), Shop("Café Central", 5),
            attempts, cars, shopCount, week: 3, year: 1924, friendshipLevel: 1);

    private static CarSpec Car(double price) =>
        new("ks_willy", 2020, "Test", "One", Price: price);

    private static Resident Person(string first, string surname) =>
        new(new AddressLocation("Street 1", 12, HouseNumberPlan.SideOf(12)),
            new ProceduralName(first, surname));

    private static PointOfInterest Shop(string name, int number) =>
        new("coffee_" + number, name,
            new AddressLocation("Street 1", number, HouseNumberPlan.SideOf(number)),
            PoiTypes.CoffeeShop);
}
