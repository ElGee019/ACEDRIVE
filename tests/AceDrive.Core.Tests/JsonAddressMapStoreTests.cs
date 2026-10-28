using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// Data/address_map.json is the operating zone's street pool (TOZONES §4.1):
/// the shipped file must stay loadable, internally consistent and able to fund
/// the whole expansion ladder. An invalid row would otherwise only surface as
/// an empty zone after a hand edit.
/// </summary>
public sealed class JsonAddressMapStoreTests
{
    // The temporary stand-in until the real free-roam map lands: short streets
    // so all of them fit on the Brands Hatch Indy lap and every house has its
    // own physical spot (a door can never be stranded on an unreachable label).
    private const int HousesPerStreet = 3;

    [Fact]
    public void ShippedMap_IsThirtyTwoContiguousThreeHouseStreets()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "address_map.json");
        Assert.True(File.Exists(file), "address_map.json is not copied to the output");

        var log = new TestLog();
        AddressMap map = new JsonAddressMapStore(file, log).Load();
        Assert.Empty(log.Warnings);

        // 1,929 m / 32 = 60.28 m per street: two 30 m gaps plus the tail, so
        // each row counts 0, 30, 60 = 3 numbers. Thirty-two is the most
        // streets that fit while every one keeps 3 houses at the shipped
        // spacing; the rows tile the whole lap, so no position lacks an
        // address and no two streets share a house's spot.
        Assert.Equal(32, map.Streets.Count);
        Assert.All(map.Streets, street => Assert.False(street.Fictional));
        Assert.All(map.Streets, street =>
            Assert.Equal(HousesPerStreet, HouseNumberPlan.Count(street, map)));
        Assert.Equal(0.0, map.Streets[0].FromFraction);
        Assert.Equal(1.0, map.Streets[^1].ToFraction);
        for (int i = 1; i < map.Streets.Count; i++)
        {
            Assert.Equal(map.Streets[i - 1].ToFraction, map.Streets[i].FromFraction);
        }

        Assert.Equal(
            map.Streets.Count,
            map.Streets.Select(street => street.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            map.Streets.Count,
            map.Streets.Select(street => street.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        // Every row is a real slice, so the base length still sums to the lap.
        Assert.Equal(1.929, AddressBook.MapLengthKm(map), precision: 9);
    }

    [Fact]
    public void ShippedMap_ResolvesEveryHouseBackToItsOwnDoor()
    {
        // The point of the short streets: every house number sits on a lap
        // position that resolves back to that same street and number, so no
        // job, venue or story door is stranded on an unreachable label.
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "address_map.json");
        AddressMap map = new JsonAddressMapStore(file, new TestLog()).Load();

        foreach (AddressStreet street in map.Streets)
        {
            int count = HouseNumberPlan.Count(street, map);
            for (int number = 1; number <= count; number++)
            {
                var expected = new AddressLocation(
                    street.Name, number, HouseNumberPlan.SideOf(number));
                Assert.Equal(
                    expected,
                    AddressResolver.Resolve(
                        MidHousePosition(street, number, map),
                        map.TrackLengthMeters, map));
            }
        }
    }

    // The middle of a house's slot, so the answer never hangs on a boundary
    // bit. The last slot is cut short by the street's tail, hence the clamp:
    // at 150 m the sixth number still owns the metres up to the street's end.
    private static double MidHousePosition(
        AddressStreet street, int number, AddressMap map)
    {
        double from = street.FromFraction * map.TrackLengthMeters;
        double to = street.ToFraction * map.TrackLengthMeters;
        double offset = from + (number - 0.5) * map.HouseSpacingMeters;
        return Math.Min(offset, to - 0.001) / map.TrackLengthMeters;
    }

    [Fact]
    public void FictionalFlag_SurvivesTheJsonRoundTrip()
    {
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
        try
        {
            File.WriteAllText(file, """
                {
                  "country": "Germany",
                  "city": "Brands",
                  "trackLengthMeters": 1929,
                  "houseSpacingMeters": 30,
                  "streets": [
                    { "id": "street-1", "name": "Street 1", "fromFraction": 0.0, "toFraction": 0.5 },
                    { "id": "street-3", "name": "Street 3", "fromFraction": 0.0, "toFraction": 0.5, "fictional": true }
                  ]
                }
                """);

            var log = new TestLog();
            AddressMap map = new JsonAddressMapStore(file, log).Load();

            Assert.Empty(log.Warnings);
            Assert.False(map.Streets[0].Fictional);
            Assert.True(map.Streets[1].Fictional);
        }
        finally
        {
            File.Delete(file);
        }
    }
}
