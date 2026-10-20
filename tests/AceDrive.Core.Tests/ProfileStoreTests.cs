using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>profile.json + address_map.json boundaries: round-trip and degrade.</summary>
public sealed class ProfileStoreTests
{
    [Fact]
    public void Profile_RoundTrips()
    {
        string file = TempFile();
        try
        {
            var log = new TestLog();
            var store = new JsonProfileStore(file, log);
            var profile = new PlayerProfile("ElGee", "Germany", "Brands", "street-2", 17);
            store.Save(profile);

            PlayerProfile? loaded = new JsonProfileStore(file, log).Load();
            Assert.Equal(profile, loaded);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Profile_MissingFile_ReturnsNull()
    {
        var log = new TestLog();
        Assert.Null(new JsonProfileStore(TempFile(), log).Load());
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void Profile_CorruptFile_ReturnsNullWithWarning()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();
            Assert.Null(new JsonProfileStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Profile_IncompleteFile_ReturnsNullWithWarning()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file,
                """{"DisplayName":"","Country":"Germany","City":"Brands","StreetId":"street-1","HouseNumber":1}""");
            var log = new TestLog();
            Assert.Null(new JsonProfileStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Profile_RejectsIncompleteSave()
    {
        string file = TempFile();
        try
        {
            var log = new TestLog();
            new JsonProfileStore(file, log).Save(
                new PlayerProfile("", "Germany", "Brands", "street-1", 1));
            Assert.False(File.Exists(file));
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void AddressMap_ValidFile_Loads()
    {
        string file = TempFile();
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
                    { "id": "street-2", "name": "Street 2", "fromFraction": 0.5, "toFraction": 1.0 }
                  ]
                }
                """);
            var log = new TestLog();
            AddressMap map = new JsonAddressMapStore(file, log).Load();
            Assert.Equal("Germany", map.Country);
            Assert.Equal("Brands", map.City);
            Assert.Equal(1929.0, map.TrackLengthMeters);
            Assert.Equal(2, map.Streets.Count);
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void AddressMap_MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();
        AddressMap map = new JsonAddressMapStore(TempFile(), log).Load();
        Assert.Equal(AddressMap.Default, map);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void AddressMap_InvalidFile_FallsBackWithWarning()
    {
        string file = TempFile();
        try
        {
            File.WriteAllText(file, """
                {
                  "country": "Germany",
                  "city": "Brands",
                  "trackLengthMeters": 0,
                  "houseSpacingMeters": 30,
                  "streets": [
                    { "id": "street-1", "name": "Street 1", "fromFraction": 0.0, "toFraction": 0.5 }
                  ]
                }
                """);
            var log = new TestLog();
            AddressMap map = new JsonAddressMapStore(file, log).Load();
            Assert.Equal(AddressMap.Default, map);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
}
