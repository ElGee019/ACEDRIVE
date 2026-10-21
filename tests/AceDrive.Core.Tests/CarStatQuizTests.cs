using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The car-stat quiz (PORTPLAN item 11, Unity ProceduralQuestionService):
/// stat reads, Unity band edges, decoy preference/dedupe/padding, shuffle
/// consistency and the asked-category reset at the category count.</summary>
public sealed class CarStatQuizTests
{
    private static readonly CarStatCategory Weight = CustomerRequestTestData.Weight;

    private static readonly CarStatCategory Power = new(
        "power", CarStat.Horsepower, "QuizUnitHp",
        [
            new CarStatBand("high", ["High?"], Min: 200),
            new CarStatBand("normal", ["Normal?"]),
        ]);

    [Fact]
    public void Read_AllSixStats()
    {
        CarSpec car = CustomerRequestTestData.Car(
            mass: 1400, horsepower: 240, topSpeed: 220, engineCc: 2000,
            fuel: 8.4, price: 45000.0);

        Assert.Equal(1400.0, CarStatQuiz.Read(car, CarStat.MassKg));
        Assert.Equal(240.0, CarStatQuiz.Read(car, CarStat.Horsepower));
        Assert.Equal(220.0, CarStatQuiz.Read(car, CarStat.TopSpeedKmh));
        Assert.Equal(2000.0, CarStatQuiz.Read(car, CarStat.EngineCc));
        Assert.Equal(8.4, CarStatQuiz.Read(car, CarStat.FuelPer100Km));
        Assert.Equal(45000.0, CarStatQuiz.Read(car, CarStat.Price));
    }

    [Fact]
    public void Read_ZeroOrMissing_ReturnsNull()
    {
        Assert.Null(CarStatQuiz.Read(null, CarStat.MassKg));
        Assert.Null(CarStatQuiz.Read(CustomerRequestTestData.Car(mass: 0), CarStat.MassKg));
        Assert.Null(CarStatQuiz.Read(CustomerRequestTestData.Car(price: 0.0), CarStat.Price));
    }

    [Theory]
    [InlineData(1800.0, "medium")]
    [InlineData(1801.0, "heavy")]
    [InlineData(1200.0, "medium")]
    [InlineData(1199.0, "light")]
    [InlineData(1400.0, "medium")]
    public void WeightBands_KeepUnityEdges(double value, string tier)
    {
        Assert.Equal(tier, Weight.BandFor(value)!.Tier);
    }

    [Theory]
    [InlineData(200.0, "normal")]
    [InlineData(201.0, "high")]
    public void PowerBands_KeepTheEdge(double value, string tier)
    {
        Assert.Equal(tier, Power.BandFor(value)!.Tier);
    }

    [Fact]
    public void OtherBands_MatchUnityThresholds()
    {
        var topSpeed = new CarStatCategory("topSpeed", CarStat.TopSpeedKmh, "QuizUnitKmh",
            [new CarStatBand("high", ["High?"], Min: 180), new CarStatBand("normal", ["Normal?"])]);
        var engine = new CarStatCategory("engineSize", CarStat.EngineCc, "QuizUnitCc",
            [new CarStatBand("large", ["Large?"], Min: 2500), new CarStatBand("small", ["Small?"])]);
        var fuel = new CarStatCategory("fuelConsumption", CarStat.FuelPer100Km, "QuizUnitFuel",
        [
            new CarStatBand("efficient", ["Efficient?"], Max: 6),
            new CarStatBand("thirsty", ["Thirsty?"], Min: 10),
            new CarStatBand("moderate", ["Moderate?"]),
        ]);
        var price = new CarStatCategory("price", CarStat.Price, "QuizUnitEuro",
        [
            new CarStatBand("luxury", ["Luxury?"], Min: 80000),
            new CarStatBand("budget", ["Budget?"], Max: 30000),
            new CarStatBand("mid", ["Mid?"]),
        ]);

        Assert.Equal("high", topSpeed.BandFor(180.1)!.Tier);
        Assert.Equal("normal", topSpeed.BandFor(180.0)!.Tier);
        Assert.Equal("large", engine.BandFor(2501)!.Tier);
        Assert.Equal("small", engine.BandFor(2500)!.Tier);
        Assert.Equal("efficient", fuel.BandFor(5.9)!.Tier);
        Assert.Equal("moderate", fuel.BandFor(6.0)!.Tier);
        Assert.Equal("moderate", fuel.BandFor(10.0)!.Tier);
        Assert.Equal("thirsty", fuel.BandFor(10.1)!.Tier);
        Assert.Equal("luxury", price.BandFor(80001)!.Tier);
        Assert.Equal("mid", price.BandFor(80000)!.Tier);
        Assert.Equal("mid", price.BandFor(30000)!.Tier);
        Assert.Equal("budget", price.BandFor(29999)!.Tier);
    }

    [Fact]
    public void TryCreate_PrefersSameYearDecoysAndDedupes()
    {
        CarSpec car = CustomerRequestTestData.Car(year: 2020, mass: 1400);
        CarSpec sameYearA = CustomerRequestTestData.Car(year: 2020, mass: 1500);
        CarSpec sameYearB = CustomerRequestTestData.Car(year: 2020, mass: 1600);
        CarSpec otherYear = CustomerRequestTestData.Car(year: 1990, mass: 1900);
        // The duplicate mass must not consume a decoy slot.
        CarSpec duplicate = CustomerRequestTestData.Car(year: 2021, mass: 1500);

        CarStatQuestion question = Assert.IsType<CarStatQuestion>(
            CarStatQuiz.TryCreate(
                Weight, car, [car, sameYearA, sameYearB, otherYear, duplicate],
                new Random(0)));

        Assert.Equal(3, question.Options.Count);
        Assert.Equal(3, question.Options.Distinct().Count());
        Assert.Contains(1400.0, question.Options);
        Assert.Contains(1500.0, question.Options);
        Assert.Contains(1600.0, question.Options);
        Assert.DoesNotContain(1900.0, question.Options);
        Assert.Equal(1400.0, question.Options[question.CorrectIndex]);
    }

    [Fact]
    public void TryCreate_PadsWhenThePoolCannotSupplyTwoDecoys()
    {
        CarSpec car = CustomerRequestTestData.Car(mass: 1400);

        CarStatQuestion question = Assert.IsType<CarStatQuestion>(
            CarStatQuiz.TryCreate(Weight, car, [car], new Random(3)));

        Assert.Equal(3, question.Options.Count);
        Assert.Equal(3, question.Options.Distinct().Count());
        Assert.All(question.Options, value => Assert.True(value > 0.0));
        Assert.Equal(1400.0, question.Options[question.CorrectIndex]);
    }

    [Fact]
    public void TryCreate_TinyValue_NeverPadsBelowZero()
    {
        CarSpec car = CustomerRequestTestData.Car(mass: 12);

        CarStatQuestion question = Assert.IsType<CarStatQuestion>(
            CarStatQuiz.TryCreate(Weight, car, [car], new Random(0)));

        Assert.All(question.Options, value => Assert.True(value > 0.0));
        Assert.Equal(12.0, question.Options[question.CorrectIndex]);
    }

    [Fact]
    public void TryCreate_MissingStatOrBand_ReturnsNull()
    {
        CarSpec noMass = CustomerRequestTestData.Car(mass: 0);
        Assert.Null(CarStatQuiz.TryCreate(Weight, noMass, [noMass], new Random(0)));

        var noBand = new CarStatCategory(
            "weight", CarStat.MassKg, "QuizUnitKg",
            [new CarStatBand("heavy", ["Heavy?"], Min: 3000)]);
        CarSpec car = CustomerRequestTestData.Car(mass: 1400);
        Assert.Null(CarStatQuiz.TryCreate(noBand, car, [car], new Random(0)));
    }

    [Fact]
    public void AvailableCategories_ExcludesAskedCaseInsensitively()
    {
        IReadOnlyList<CarStatCategory> available =
            CarStatQuiz.AvailableCategories([Weight, Power], ["WEIGHT"]);

        Assert.Same(Power, Assert.Single(available));
    }

    [Fact]
    public void MarkAsked_AppendsThenResetsAtTheCategoryCount()
    {
        IReadOnlyList<string> one = CarStatQuiz.MarkAsked([], "weight", categoryCount: 6);
        Assert.Equal(["weight"], one);

        IReadOnlyList<string> five = ["weight", "power", "topSpeed", "engineSize", "fuelConsumption"];
        IReadOnlyList<string> reset = CarStatQuiz.MarkAsked(five, "price", categoryCount: 6);
        Assert.Empty(reset);
    }

    [Fact]
    public void MarkAsked_DedupesAndKeepsCase()
    {
        IReadOnlyList<string> asked = CarStatQuiz.MarkAsked(
            ["weight", " weight ", "power"], "Weight", categoryCount: 6);

        Assert.Equal(["weight", "power"], asked);
    }
}
