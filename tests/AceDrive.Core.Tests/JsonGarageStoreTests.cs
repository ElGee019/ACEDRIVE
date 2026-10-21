using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>garage.json load rules (PORTPLAN item 30): a missing or corrupt file
/// degrades to an empty garage, unusable rows drop with a warning, and a current
/// key no car backs clears instead of pointing at nothing.</summary>
public sealed class JsonGarageStoreTests
{
    private static string Write(string json)
    {
        string file = Path.GetTempFileName();
        File.WriteAllText(file, json);
        return file;
    }

    [Fact]
    public void RoundTrip_KeepsCarsAndCurrentKey()
    {
        string file = Path.GetTempFileName();
        try
        {
            var store = new JsonGarageStore(file, new TestLog());
            store.Save(new GarageState("ks_mazda_mx5_na", [
                new OwnedCarState("ks_mazda_mx5_na", 3, 41.5, true, 7),
                new OwnedCarState("ks_alfa_romeo_junior", 9, 0.0, false),
            ]));

            GarageState? state = new JsonGarageStore(file, new TestLog()).Load();
            Assert.NotNull(state);
            Assert.Equal("ks_mazda_mx5_na", state.CurrentKey);
            Assert.Equal(2, state.Cars!.Count);
            Assert.Equal(41.5, state.Cars[0].Odometer);
            Assert.True(state.Cars[0].HasInsurance);
            // The filth reading rides the same row (PORTPLAN item 33).
            Assert.Equal(7, state.Cars[0].Filth);
            Assert.Equal(0, state.Cars[1].Filth);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void MissingFile_MeansAnEmptyGarage()
    {
        string file = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Assert.Null(new JsonGarageStore(file, new TestLog()).Load());
    }

    [Fact]
    public void CorruptFile_DegradesWithAWarning()
    {
        string file = Write("{ not json");
        try
        {
            var log = new TestLog();
            Assert.Null(new JsonGarageStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void RoundTrip_KeepsAskedQuizCategories()
    {
        string file = Path.GetTempFileName();
        try
        {
            var store = new JsonGarageStore(file, new TestLog());
            store.Save(new GarageState("ks_mazda_mx5_na", [
                new OwnedCarState("ks_mazda_mx5_na", AskedCategories: ["weight", "power"]),
            ]));

            GarageState? state = new JsonGarageStore(file, new TestLog()).Load();
            Assert.NotNull(state);
            Assert.Equal(["weight", "power"], Assert.Single(state.Cars!).AskedCategories!);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void PreQuizSave_LoadsAnEmptyAskedHistory()
    {
        // Every garage.json written before the request slice has no asked
        // categories: the car must load with an empty history, not fail.
        string file = Write("""
            {"currentKey":"ks_mazda_mx5_na","cars":[
              {"key":"ks_mazda_mx5_na","purchaseWeek":1,"odometer":4,"hasInsurance":false}
            ]}
            """);
        try
        {
            GarageState? state = new JsonGarageStore(file, new TestLog()).Load();
            Assert.NotNull(state);
            OwnedCarState row = Assert.Single(state.Cars!);
            Assert.Null(row.AskedCategories);
            var car = new OwnedCar(row.Key, row.PurchaseWeek, saved: row);
            Assert.Empty(car.AskedCategories);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void OwnedCar_MarksAndResetsAtTheCategoryCount()
    {
        var car = new OwnedCar("ks_mazda_mx5_na", 1);

        car.MarkAskedCategory("weight", categoryCount: 2);
        Assert.Equal(["weight"], car.AskedCategories);

        // The second of two categories completes the set and clears it.
        car.MarkAskedCategory("power", categoryCount: 2);
        Assert.Empty(car.AskedCategories);

        car.MarkAskedCategory("weight", categoryCount: 2);
        Assert.Equal(["weight"], car.Snapshot().AskedCategories!);
    }

    [Fact]
    public void UnusableRows_DropWithAWarning()
    {
        // No key, a duplicate key, a negative odometer: each would corrupt a
        // per-car number later (a car owned twice double-counts everything),
        // so the store drops them and says so.
        string file = Write("""
            {"currentKey":"ks_gone","cars":[
              {"key":"","purchaseWeek":1,"odometer":0,"hasInsurance":false},
              {"key":"ks_mazda_mx5_na","purchaseWeek":3,"odometer":-1,"hasInsurance":false},
              {"key":"ks_mazda_mx5_na","purchaseWeek":4,"odometer":12,"hasInsurance":true},
              {"key":"ks_alfa_romeo_junior","purchaseWeek":-2,"odometer":3,"hasInsurance":false},
              {"key":"ks_alfa_romeo_junior","purchaseWeek":1,"odometer":7,"hasInsurance":false}
            ]}
            """);
        try
        {
            var log = new TestLog();
            GarageState? state = new JsonGarageStore(file, log).Load();
            Assert.NotNull(state);
            Assert.Equal(2, state.Cars!.Count);
            Assert.Equal("ks_mazda_mx5_na", state.Cars[0].Key);
            Assert.Equal(12.0, state.Cars[0].Odometer);
            Assert.True(state.Cars[0].HasInsurance);
            Assert.Equal(0, state.Cars[1].PurchaseWeek);
            // The current key names a car the file does not hold: cleared, so
            // the Garage falls back to its own first car.
            Assert.Null(state.CurrentKey);
            Assert.Contains(log.Warnings, w => w.Contains("3 unusable", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void EmptyFile_MeansAnEmptyGarage()
    {
        string file = Write("{}");
        try
        {
            GarageState? state = new JsonGarageStore(file, new TestLog()).Load();
            Assert.NotNull(state);
            Assert.Null(state.CurrentKey);
            Assert.Empty(state.Cars!);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void PreFilthSave_LoadsClean()
    {
        // Every garage.json written before PORTPLAN item 33 has no filth
        // column: the record default keeps the car clean instead of dropping it.
        string file = Write("""
            {"currentKey":"ks_mazda_mx5_na","cars":[
              {"key":"ks_mazda_mx5_na","purchaseWeek":1,"odometer":4,"hasInsurance":false}
            ]}
            """);
        try
        {
            GarageState? state = new JsonGarageStore(file, new TestLog()).Load();
            Assert.NotNull(state);
            Assert.Equal(0, Assert.Single(state.Cars!).Filth);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void RoundTrip_KeepsConditionAndEngine()
    {
        string file = Path.GetTempFileName();
        try
        {
            var weekly = new DateTime(2027, 1, 4, 8, 30, 0, DateTimeKind.Utc);
            var store = new JsonGarageStore(file, new TestLog());
            store.Save(new GarageState("ks_mazda_mx5_na", [
                new OwnedCarState("ks_mazda_mx5_na", 3, 41.5, true, 7,
                    [new MaintenancePartState("tires", 12.0)],
                    new EngineConditionState(11.0, Broken: true, WeeklyNext: weekly)),
            ]));

            GarageState? state = new JsonGarageStore(file, new TestLog()).Load();
            Assert.NotNull(state);
            OwnedCarState car = Assert.Single(state.Cars!);
            Assert.Equal(12.0, Assert.Single(car.Parts!).LastServiceKm);
            Assert.True(car.Engine!.Broken);
            Assert.Equal(11.0, car.Engine.BaseOdometer);
            Assert.Equal(weekly, car.Engine.WeeklyNext);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void PreConditionSave_LoadsWithoutPartsOrEngine()
    {
        // Every garage.json written before PORTPLAN items 31/32 has neither a
        // part deck nor an engine block: both default to null so the car loads
        // and the CarCondition backfills at the current odometer.
        string file = Write("""
            {"currentKey":"ks_mazda_mx5_na","cars":[
              {"key":"ks_mazda_mx5_na","purchaseWeek":1,"odometer":4,"hasInsurance":false,"filth":2}
            ]}
            """);
        try
        {
            GarageState? state = new JsonGarageStore(file, new TestLog()).Load();
            Assert.NotNull(state);
            OwnedCarState car = Assert.Single(state.Cars!);
            Assert.Null(car.Parts);
            Assert.Null(car.Engine);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void UnusablePartRows_Drop()
    {
        string file = Write("""
            {"currentKey":"ks_mazda_mx5_na","cars":[
              {"key":"ks_mazda_mx5_na","purchaseWeek":1,"odometer":4,"hasInsurance":false,
               "parts":[
                 {"key":"","lastServiceKm":0},
                 {"key":"tires","lastServiceKm":-1},
                 {"key":"tires","lastServiceKm":12},
                 {"key":"tires","lastServiceKm":30},
                 {"key":"oil_change","lastServiceKm":5}
               ],
               "engine":{"baseOdometer":-9,"broken":false}}
            ]}
            """);
        try
        {
            GarageState? state = new JsonGarageStore(file, new TestLog()).Load();
            Assert.NotNull(state);
            OwnedCarState car = Assert.Single(state.Cars!);
            Assert.Equal(2, car.Parts!.Count);
            Assert.Equal("tires", car.Parts[0].Key);
            Assert.Equal(12.0, car.Parts[0].LastServiceKm); // the first valid duplicate wins
            Assert.Equal(0.0, car.Engine!.BaseOdometer);  // the negative base clamps
        }
        finally
        {
            File.Delete(file);
        }
    }
}
