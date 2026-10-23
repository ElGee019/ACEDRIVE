using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Small request fixtures: one car, one weight category, and a table
/// whose chances each test overrides.</summary>
internal static class CustomerRequestTestData
{
    public static CarSpec Car(
        int year = 2020, int mass = 1400, int horsepower = 240,
        int topSpeed = 200, int engineCc = 2000,
        double fuel = 8.0, double price = 45000.0) =>
        new($"ks_quiz_{year}_{mass}", year, "Test", "Quiz",
            MassKg: mass, Horsepower: horsepower, TopSpeedKmh: topSpeed,
            EngineCc: engineCc, FuelPer100Km: fuel, Price: price);

    public static CarStatCategory Weight { get; } = new(
        "weight", CarStat.MassKg, "QuizUnitKg",
        [
            new CarStatBand("heavy", ["Heavy?"], Min: 1800),
            new CarStatBand("light", ["Light?"], Max: 1200),
            new CarStatBand("medium", ["Medium?"]),
        ]);

    public static IReadOnlyList<CarStatCategory> Categories { get; } = [Weight];

    /// <summary>The full shipped category count, so a mark does not trigger
    /// the at-count reset (the host passes the table's count).</summary>
    public static IReadOnlyList<CarStatCategory> SixCategories { get; } =
    [
        Weight,
        new("power", CarStat.Horsepower, "QuizUnitHp",
            [new CarStatBand("normal", ["How much power?"])]),
        new("topSpeed", CarStat.TopSpeedKmh, "QuizUnitKmh",
            [new CarStatBand("normal", ["How fast?"])]),
        new("engineSize", CarStat.EngineCc, "QuizUnitCc",
            [new CarStatBand("normal", ["How big is the engine?"])]),
        new("fuelConsumption", CarStat.FuelPer100Km, "QuizUnitFuel",
            [new CarStatBand("normal", ["How thirsty?"])]),
        new("price", CarStat.Price, "QuizUnitEuro",
            [new CarStatBand("normal", ["What does it cost?"])]),
    ];

    /// <summary>A passive ask the flow can accept for a tip.</summary>
    public static CustomerRequest PassiveRequest { get; } =
        new(CustomerRequestKind.Passive, "Could you avoid the potholes?");

    /// <summary>A speed challenge, for the police/fine flow.</summary>
    public static CustomerRequest SpeedRequest { get; } =
        new(CustomerRequestKind.SpeedChallenge, "Please get there quickly!");

    public static CustomerRequestTable Table(
        double tier2 = 0.0, double tier3 = 0.0,
        IReadOnlyDictionary<string, string>? suffixes = null,
        IReadOnlyList<CarStatCategory>? categories = null) =>
        new(
            suffixes ?? new Dictionary<string, string>(StringComparer.Ordinal),
            categories ?? Categories,
            SpeedChallengeText: "Quickly!",
            Tier2Chance: tier2,
            Tier3Chance: tier3,
            TimerSeconds: 19);
}
