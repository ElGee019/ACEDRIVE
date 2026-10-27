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
    [Fact]
    public void ShippedMap_CarriesTheBasePairPlusTheFictionalPool()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "address_map.json");
        Assert.True(File.Exists(file), "address_map.json is not copied to the output");

        var log = new TestLog();
        AddressMap map = new JsonAddressMapStore(file, log).Load();
        Assert.Empty(log.Warnings);

        // Two real streets (the placeholder base) and eight fictional labels,
        // enough for nine expansions from a fresh career (TOZONES §4.1).
        Assert.Equal(10, map.Streets.Count);
        Assert.Equal(2, map.Streets.Count(street => !street.Fictional));
        Assert.Equal(8, map.Streets.Count(street => street.Fictional));
        Assert.Equal("Street 1", map.Streets[0].Name);
        Assert.Equal("Street 2", map.Streets[1].Name);
        Assert.Equal(
            map.Streets.Count,
            map.Streets.Select(street => street.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            map.Streets.Count,
            map.Streets.Select(street => street.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(map.Streets, street =>
        {
            Assert.InRange(street.FromFraction, 0.0, 1.0);
            Assert.InRange(street.ToFraction, 0.0, 1.0);
            Assert.True(street.ToFraction > street.FromFraction, street.Id);
        });
        // Fictional rows reuse the lap and never grow the base area.
        Assert.Equal(1.929, AddressBook.MapLengthKm(map), precision: 9);
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
