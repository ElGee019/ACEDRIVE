using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Normalised lap position -> placeholder street and house number.</summary>
public sealed class AddressResolverTests
{
    private static readonly AddressMap Map = AddressMap.Default;

    [Fact]
    public void StartLine_IsStreetOneNumberOne()
    {
        Assert.Equal(
            new AddressLocation("Street 1", 1, HouseSide.Right),
            AddressResolver.Resolve(0.0, 1929.0, Map));
    }

    [Fact]
    public void QuarterLap_AdvancesEvery30Metres()
    {
        // 0.25 * 1929 = 482.25 m into Street 1: 482.25 / 30 = 16.075 -> No. 17.
        Assert.Equal(
            new AddressLocation("Street 1", 17, HouseSide.Right),
            AddressResolver.Resolve(0.25, 1929.0, Map));
    }

    [Fact]
    public void HalfLap_FlipsToStreetTwoNumberOne()
    {
        Assert.Equal(
            new AddressLocation("Street 2", 1, HouseSide.Right),
            AddressResolver.Resolve(0.5, 1929.0, Map));
    }

    [Fact]
    public void LapEnd_StaysOnTheLastStreet()
    {
        Assert.Equal(
            new AddressLocation("Street 2", 33, HouseSide.Right),
            AddressResolver.Resolve(1.0, 1929.0, Map));
    }

    [Fact]
    public void RuntimeTrackLength_OverridesTheDataLength()
    {
        var map = new AddressMap(
            "Germany", "Brands", 300.0, 30.0,
            [new AddressStreet("s", "S", 0.0, 1.0)]);

        // 0.25 of 600 m = 150 m -> No. 6; the map's own 300 m would say No. 3.
        Assert.Equal(
            new AddressLocation("S", 6, HouseSide.Left),
            AddressResolver.Resolve(0.25, 600.0, map));
    }

    [Fact]
    public void MissingTrackLength_FallsBackToTheMap()
    {
        Assert.Equal(
            new AddressLocation("Street 1", 17, HouseSide.Right),
            AddressResolver.Resolve(0.25, 0.0, Map));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidPosition_AnswersNull(double position)
    {
        Assert.Null(AddressResolver.Resolve(position, 1929.0, Map));
    }

    [Fact]
    public void InvalidSpacing_AnswersNull()
    {
        var map = new AddressMap(
            "Germany", "Brands", 300.0, 0.0,
            [new AddressStreet("s", "S", 0.0, 1.0)]);
        Assert.Null(AddressResolver.Resolve(0.5, 300.0, map));
    }

    [Fact]
    public void MapWithoutStreets_AnswersNull()
    {
        var map = new AddressMap("Germany", "Brands", 300.0, 30.0, []);
        Assert.Null(AddressResolver.Resolve(0.5, 300.0, map));
    }
}
