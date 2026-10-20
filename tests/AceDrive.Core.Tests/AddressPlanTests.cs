using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>House-number rule for the placeholder free-roam map.</summary>
public sealed class AddressPlanTests
{
    [Fact]
    public void DefaultMap_IsTheBrandsHatchStandIn()
    {
        Assert.Equal("Germany", AddressMap.Default.Country);
        Assert.Equal("Brands", AddressMap.Default.City);
        Assert.Equal(1929.0, AddressMap.Default.TrackLengthMeters);
        Assert.Equal(30.0, AddressMap.Default.HouseSpacingMeters);
        Assert.Equal(2, AddressMap.Default.Streets.Count);
        Assert.Equal("Street 1", AddressMap.Default.Streets[0].Name);
        Assert.Equal("Street 2", AddressMap.Default.Streets[1].Name);
    }

    [Fact]
    public void BothStreets_Get33Numbers()
    {
        // 1929 m / 2 = 964.5 m per street; number 1 sits at 0 m, so the
        // dropdown counts 0, 30, ..., 960 = 33 entries.
        Assert.Equal(33, HouseNumberPlan.Count(AddressMap.Default.Streets[0], AddressMap.Default));
        Assert.Equal(33, HouseNumberPlan.Count(AddressMap.Default.Streets[1], AddressMap.Default));
        Assert.Equal(33, HouseNumberPlan.Numbers(AddressMap.Default.Streets[0], AddressMap.Default).Count);
    }

    [Fact]
    public void Numbers_RunFromOneThroughCount()
    {
        IReadOnlyList<int> numbers = HouseNumberPlan.Numbers(
            AddressMap.Default.Streets[0], AddressMap.Default);
        Assert.Equal(1, numbers[0]);
        Assert.Equal(33, numbers[^1]);
    }

    [Fact]
    public void Sides_AreEvenLeftOddRight()
    {
        // Unity HouseNumberGenerator parity: parity picks the side, never the
        // count — the street keeps its 33 numbers.
        Assert.Equal(HouseSide.Right, HouseNumberPlan.SideOf(1));
        Assert.Equal(HouseSide.Left, HouseNumberPlan.SideOf(2));
        Assert.Equal(HouseSide.Right, HouseNumberPlan.SideOf(33));
    }

    [Fact]
    public void Offsets_StartAtZeroAndStep30m()
    {
        AddressStreet street = AddressMap.Default.Streets[0];
        Assert.Equal(0.0, HouseNumberPlan.OffsetMeters(street, 1, AddressMap.Default));
        Assert.Equal(30.0, HouseNumberPlan.OffsetMeters(street, 2, AddressMap.Default));
        Assert.Equal(960.0, HouseNumberPlan.OffsetMeters(street, 33, AddressMap.Default));
        Assert.Equal(-1.0, HouseNumberPlan.OffsetMeters(street, 34, AddressMap.Default));
        Assert.Equal(-1.0, HouseNumberPlan.OffsetMeters(street, 0, AddressMap.Default));
    }

    [Fact]
    public void SpacingChange_RecalculatesTheDropdown()
    {
        var map = new AddressMap(
            "Germany", "Brands", 300.0, 30.0,
            [new AddressStreet("s", "S", 0.0, 1.0)]);
        Assert.Equal(11, HouseNumberPlan.Count(map.Streets[0], map));
        Assert.Equal(300.0, HouseNumberPlan.OffsetMeters(map.Streets[0], 11, map));
    }

    [Fact]
    public void InvalidSpacing_AnswersEmpty()
    {
        var map = new AddressMap(
            "Germany", "Brands", 300.0, 0.0,
            [new AddressStreet("s", "S", 0.0, 1.0)]);
        Assert.Equal(0, HouseNumberPlan.Count(map.Streets[0], map));
        Assert.Empty(HouseNumberPlan.Numbers(map.Streets[0], map));
    }

    [Fact]
    public void MissingStreetGeometry_AnswersEmpty()
    {
        var map = new AddressMap(
            "Germany", "Brands", 300.0, 30.0,
            [new AddressStreet("s", "S", 0.5, 0.5)]);
        Assert.Equal(0, HouseNumberPlan.Count(map.Streets[0], map));
    }
}
