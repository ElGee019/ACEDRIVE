using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The offer draw: an unlocked brand, car inside the loyalty band (or
/// the nearest class of that brand), a resolvable dealer door, and a customer
/// door away from the dealer and the home street.</summary>
public sealed class CarHaulOfferTests
{
    private static readonly AddressMap ThreeStreets = new(
        "Germany", "Brands", 1929.0, 30.0,
        [new AddressStreet("s1", "Street 1", 0.0, 0.3),
         new AddressStreet("s2", "Street 2", 0.3, 0.6),
         new AddressStreet("s3", "Street 3", 0.6, 1.0)]);

    [Fact]
    public void Draw_PicksALoyalBrandsCarInsideItsBand()
    {
        CarHaulOffer? offer = Draw(
            Cars(("Abarth", 3), ("Abarth", 6), ("Abarth", 7), ("Abarth", 9), ("Puma", 4)),
            loyalty: brand => brand == "Abarth" ? 250.0 : 0.0);

        Assert.NotNull(offer);
        Assert.Equal("Abarth", offer.Car.Brand);
        Assert.InRange(offer.Car.Class, 1, 8);
        Assert.Equal(250.0, offer.BrandLoyaltyPercent);
    }

    [Fact]
    public void Draw_NamesTheDrawnCarClassTier()
    {
        CarHaulOffer? offer = Draw(
            Cars(("Abarth", 15)), loyalty: _ => 100.0);

        Assert.NotNull(offer);
        Assert.Equal(6, offer.PayoutTier);
    }

    [Fact]
    public void Draw_KeepsTheCustomerAwayFromDealerAndHome()
    {
        CarHaulOffer? offer = Draw(
            Cars(("Abarth", 3)),
            dealers: [Dealer("dealer-1", "Street 1")],
            home: new AddressLocation("Street 2", 2, HouseSide.Right),
            loyalty: _ => 250.0);

        Assert.NotNull(offer);
        Assert.Equal("Street 3", offer.CustomerAddress.StreetName);
    }

    [Fact]
    public void Draw_FallsBackToTheNearestClassOfTheDrawnBrand()
    {
        // Band 1-6 from 250% loyalty, the brand only makes a class-18 car:
        // Unity's first fallback keeps the brand and takes the nearest class.
        CarHaulOffer? offer = Draw(
            Cars(("Abarth", 18)), loyalty: _ => 250.0);

        Assert.NotNull(offer);
        Assert.Equal(18, offer.Car.Class);
    }

    [Fact]
    public void Draw_OpensTheTopBandAtNineHundredPercent()
    {
        CarHaulOffer? offer = Draw(
            Cars(("Abarth", 5), ("Abarth", 18)), loyalty: _ => 950.0);

        Assert.NotNull(offer);
        Assert.Equal(18, offer.Car.Class);
    }

    [Fact]
    public void Draw_FailsForANeverDrivenBrand()
    {
        Assert.Null(Draw(Cars(("Abarth", 3)), loyalty: _ => 0.0));
    }

    [Fact]
    public void Draw_UnityPolicyFailsBelowTheLoyaltyFloor()
    {
        Assert.Null(Draw(
            Cars(("Abarth", 3)), loyalty: _ => 99.9,
            policy: CarHaulPolicy.Unity));
    }

    [Fact]
    public void Draw_UnityPolicyStillDrawsAtTheFloor()
    {
        Assert.NotNull(Draw(
            Cars(("Abarth", 3)), loyalty: _ => 100.0,
            policy: CarHaulPolicy.Unity));
    }

    [Fact]
    public void Draw_FailsWithoutADealerDoor()
    {
        Assert.Null(Draw(
            Cars(("Abarth", 3)), dealers: [], loyalty: _ => 250.0));
    }

    [Fact]
    public void Draw_IgnoresDealersWithoutAStreet()
    {
        var noStreet = new PointOfInterest(
            "dealer-x", "X Cars", default, PoiTypes.Dealer);

        Assert.Null(Draw(
            Cars(("Abarth", 3)), dealers: [noStreet], loyalty: _ => 250.0));
    }

    [Fact]
    public void Draw_IgnoresOtherBusinessTypes()
    {
        var grocery = new PointOfInterest(
            "grocery-1", "Max", new AddressLocation("Street 1", 2, HouseSide.Right),
            PoiTypes.Grocery);

        Assert.Null(Draw(
            Cars(("Abarth", 3)), dealers: [grocery], loyalty: _ => 250.0));
    }

    [Fact]
    public void Draw_ResolvesTheCustomerOnceAtTheDrawnDoor()
    {
        int calls = 0;
        AddressLocation seen = default;

        CarHaulOffer? offer = Draw(
            Cars(("Abarth", 3)),
            loyalty: _ => 250.0,
            customerAt: address =>
            {
                calls++;
                seen = address;
                return new ProceduralName("Klaus", "Berger");
            });

        Assert.NotNull(offer);
        Assert.Equal(1, calls);
        Assert.Equal(offer.CustomerAddress, seen);
        Assert.Equal("Klaus Berger", offer.Customer.BaseName);
    }

    [Fact]
    public void Draw_ReplaysTheSameOfferForTheSameSeed()
    {
        CarHaulOffer? first = Draw(
            Cars(("Abarth", 3), ("Abarth", 9), ("Puma", 4)),
            loyalty: brand => brand == "Abarth" ? 700.0 : 0.0,
            seed: 1234);

        CarHaulOffer? second = Draw(
            Cars(("Abarth", 3), ("Abarth", 9), ("Puma", 4)),
            loyalty: brand => brand == "Abarth" ? 700.0 : 0.0,
            seed: 1234);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Car.Key, second.Car.Key);
        Assert.Equal(first.Dealer.Id, second.Dealer.Id);
        Assert.Equal(first.CustomerAddress, second.CustomerAddress);
    }

    private static CarHaulOffer? Draw(
        List<CarSpec> cars,
        Func<string, double> loyalty,
        IReadOnlyList<PointOfInterest>? dealers = null,
        AddressLocation? home = null,
        Func<AddressLocation, ProceduralName>? customerAt = null,
        CarHaulPolicy? policy = null,
        int seed = 7) =>
        new CarHaulOfferService(new Random(seed)).TryDraw(
            cars,
            dealers ?? [Dealer("dealer-1", "Street 1")],
            ThreeStreets,
            home,
            loyalty,
            customerAt ?? (_ => new ProceduralName("Klaus", "Berger")),
            policy ?? CarHaulPolicy.Development);

    private static List<CarSpec> Cars(params (string Brand, int Class)[] rows)
    {
        var cars = new List<CarSpec>(rows.Length);
        for (int index = 0; index < rows.Length; index++)
        {
            cars.Add(new CarSpec(
                $"car-{index}", 1960, rows[index].Brand, $"Model {index}",
                Class: rows[index].Class));
        }

        return cars;
    }

    private static PointOfInterest Dealer(
        string id, string street, int number = 1) =>
        new(id, $"{id} Cars",
            new AddressLocation(street, number, HouseNumberPlan.SideOf(number)),
            PoiTypes.Dealer);
}
