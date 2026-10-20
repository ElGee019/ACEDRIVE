using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>cars.json load rules (PORTPLAN 62): variant presets survive,
/// empty presets normalize to null, duplicate ledger keys drop, and a bad
/// file never throws the overlay down.</summary>
public sealed class JsonCarStoreTests
{
    private static IReadOnlyList<CarSpec> Load(string json, TestLog? log = null)
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, json);
            return new JsonCarStore(file, log ?? new TestLog()).Load();
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void PresetRows_KeepVariantAndKey()
    {
        var cars = Load("""
            [
              {"id":"ks_bmw_m2_coupe","year":2023,"brand":"BMW","model":"m2 g87 coupe","preset":"preset_m2_mech_1"},
              {"id":"ks_bmw_m2_coupe","year":2023,"brand":"BMW","model":"m2 g87 coupe m performance","preset":"preset_m2_mperf_1"}
            ]
            """);
        Assert.Equal(2, cars.Count);
        Assert.Equal("preset_m2_mperf_1", cars[1].Preset);
        Assert.Equal("ks_bmw_m2_coupe#preset_m2_mperf_1", cars[1].Key);
    }

    [Fact]
    public void EmptyPreset_NormalizesToNull()
    {
        var cars = Load("""
            [{"id":"ks_mazda_mx5_na","year":1990,"brand":"Mazda","model":"MX-5 NA","preset":""}]
            """);
        Assert.Null(Assert.Single(cars).Preset);
    }

    [Fact]
    public void DuplicateKey_DropsWithWarning()
    {
        // Two rows sharing a ledger key would double-count the bank; the
        // second must go with a log line beside the existing validation ones.
        var log = new TestLog();
        var cars = Load("""
            [
              {"id":"ks_bmw_m2_coupe","year":2023,"brand":"BMW","model":"m2 g87 coupe"},
              {"id":"ks_bmw_m2_coupe","year":2024,"brand":"BMW","model":"m2 g87 coupe dupe"}
            ]
            """, log);
        Assert.Single(cars);
        Assert.Contains(log.Warnings, w => w.Contains("unusable", StringComparison.Ordinal));
    }

    [Fact]
    public void NegativeSpecOrPrice_DropsWithWarning()
    {
        // The dealer prices on Price and the garage shows the specs: a corrupt
        // negative value must never reach either (a negative price would pay
        // the player on a purchase).
        var log = new TestLog();
        var cars = Load("""
            [
              {"id":"ks_ok","year":2020,"brand":"Test","model":"ok","price":1000,"massKg":900},
              {"id":"ks_negative_price","year":2020,"brand":"Test","model":"bad","price":-1000},
              {"id":"ks_negative_spec","year":2020,"brand":"Test","model":"bad","price":1000,"horsepower":-1}
            ]
            """, log);
        Assert.Equal("ks_ok", Assert.Single(cars).Id);
        Assert.Contains(log.Warnings, w => w.Contains("2 unusable", StringComparison.Ordinal));
    }

    [Fact]
    public void SpecRow_ParsesEveryColumn()
    {
        // The full v2/v3 spec row (PORTPLAN item 28) is the dealer's and the
        // garage's source: every column must survive the deserialize.
        var cars = Load("""
            [{"id":"ks_mazda_mx5_na","year":1990,"brand":"Mazda","model":"mx5 na",
              "passengers":1,"class":9,"price":12000,"trunkLitres":150,"horsepower":139,
              "engineCc":1598,"massKg":1098,"topSpeedKmh":185,"acceleration":5.3,
              "gears":5,"tankLitres":48,"fuelPer100Km":8,"brakeDistance":44}]
            """);
        CarSpec car = Assert.Single(cars);
        Assert.Equal(12000.0, car.Price);
        Assert.Equal(150, car.TrunkLitres);
        Assert.Equal(139, car.Horsepower);
        Assert.Equal(1598, car.EngineCc);
        Assert.Equal(1098, car.MassKg);
        Assert.Equal(185, car.TopSpeedKmh);
        Assert.Equal(5.3, car.Acceleration);
        Assert.Equal(5, car.Gears);
        Assert.Equal(48.0, car.TankLitres);
        Assert.Equal(8.0, car.FuelPer100Km);
        Assert.Equal(44.0, car.BrakeDistance);
    }

    [Fact]
    public void ShippedData_CarriesTheWholeRosterWithItsSpecRow()
    {
        // Data/cars.json is the car database (PORTPLAN item 28): the dealer
        // prices from it and the garage shows it. A row the generator could
        // not fill would surface as an unsellable car in game, so the whole
        // file is pinned here instead.
        string file = Path.Combine(AppContext.BaseDirectory, "Data", "cars.json");
        Assert.True(File.Exists(file), "cars.json is not copied to the output");

        var log = new TestLog();
        IReadOnlyList<CarSpec> cars = new JsonCarStore(file, log).Load();
        Assert.Empty(log.Warnings);
        Assert.Equal(101, cars.Count);                      // 71 folders + 30 variant rows
        Assert.Equal(30, cars.Count(c => c.Preset is not null));
        Assert.All(cars, c => Assert.True(c.Price > 0.0, $"{c.Key} has no price"));
        Assert.All(cars, c => Assert.InRange(c.Year, 1900, 2035));
        Assert.All(cars, c => Assert.InRange(c.Class, 1, 21));
        Assert.All(cars, c => Assert.True(c.Horsepower > 0, c.Key));
        Assert.All(cars, c => Assert.True(c.MassKg > 0, c.Key));
        Assert.All(cars, c => Assert.True(c.TopSpeedKmh > 0, c.Key));
        Assert.All(cars, c => Assert.True(c.Acceleration > 0.0, c.Key));

        // The base row carries the physics-verified value, the variant row its
        // own card value and price (CARSTATS_EVO.md v2 vs v3).
        CarSpec mx5 = cars.Single(c => c.Key == "ks_mazda_mx5_na");
        Assert.Equal(139, mx5.Horsepower);
        Assert.Equal(12000.0, mx5.Price);
        Assert.Equal(150, mx5.TrunkLitres);
        CarSpec eunos = cars.Single(c => c.Key == "ks_mazda_mx5_na#preset_mx5na_mech_2");
        Assert.Equal(131, eunos.Horsepower);
        Assert.Equal(18000.0, eunos.Price);
        Assert.Equal(150, eunos.TrunkLitres);               // inherited from the folder row
    }
}
