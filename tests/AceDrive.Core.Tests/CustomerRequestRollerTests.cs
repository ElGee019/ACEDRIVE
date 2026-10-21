using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The tier cascade (PORTPLAN item 11): suffix first, then the speed
/// chance, then the quiz chance; null means the ride has no request.</summary>
public sealed class CustomerRequestRollerTests
{
    private static readonly ProceduralName Customer =
        new("Max", "Weber", "and some boxing partners");

    [Fact]
    public void SuffixMatch_RollsAPassiveLineWithTheFullIdentity()
    {
        var suffixes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["and some boxing partners"] = "[name] are in a hurry.",
        };
        var roller = new CustomerRequestRoller(
            [CustomerRequestTestData.Car()],
            CustomerRequestTestData.Table(tier2: 1.0, suffixes: suffixes),
            seed: 0);

        CustomerRequest? request = roller.Roll(
            Customer, CustomerRequestTestData.Car(), 2026, []);

        Assert.NotNull(request);
        Assert.Equal(CustomerRequestKind.Passive, request.Kind);
        Assert.Equal("Max Weber and some boxing partners are in a hurry.", request.Text);
        Assert.Null(request.Quiz);
    }

    [Fact]
    public void NoSuffix_Tier2ChanceOne_RollsTheSpeedChallenge()
    {
        var roller = new CustomerRequestRoller(
            [CustomerRequestTestData.Car()],
            CustomerRequestTestData.Table(tier2: 1.0),
            seed: 0);

        CustomerRequest? request = roller.Roll(
            new ProceduralName("Max", "Weber"), CustomerRequestTestData.Car(), 2026, []);

        Assert.NotNull(request);
        Assert.Equal(CustomerRequestKind.SpeedChallenge, request.Kind);
        Assert.True(request.IsSpeedChallenge);
        Assert.Equal("Quickly!", request.Text);
    }

    [Fact]
    public void Tier3ChanceOne_RollsAQuizForTheCar()
    {
        var roller = new CustomerRequestRoller(
            [CustomerRequestTestData.Car()],
            CustomerRequestTestData.Table(tier3: 1.0),
            seed: 0);
        CarSpec car = CustomerRequestTestData.Car();

        CustomerRequest? request = roller.Roll(
            new ProceduralName("Max", "Weber"), car, 2026, []);

        Assert.NotNull(request);
        Assert.Equal(CustomerRequestKind.CarQuiz, request.Kind);
        CarStatQuestion quiz = Assert.IsType<CarStatQuestion>(request.Quiz);
        Assert.Equal("weight", quiz.Category.Key);
        Assert.Equal(3, quiz.Options.Count);
        Assert.InRange(quiz.CorrectIndex, 0, 2);
        Assert.Equal(1400.0, quiz.Options[quiz.CorrectIndex]);
        Assert.Contains(quiz.Text, CustomerRequestTestData.Weight.Bands.SelectMany(b => b.Variants));
    }

    [Fact]
    public void NoCar_NoRequest()
    {
        var roller = new CustomerRequestRoller(
            [CustomerRequestTestData.Car()],
            CustomerRequestTestData.Table(tier3: 1.0),
            seed: 0);

        Assert.Null(roller.Roll(new ProceduralName("Max", "Weber"), car: null, 2026, []));
    }

    [Fact]
    public void EveryCategoryAsked_NoRequest()
    {
        var roller = new CustomerRequestRoller(
            [CustomerRequestTestData.Car()],
            CustomerRequestTestData.Table(tier3: 1.0),
            seed: 0);

        Assert.Null(roller.Roll(
            new ProceduralName("Max", "Weber"), CustomerRequestTestData.Car(), 2026,
            ["weight"]));
    }

    [Fact]
    public void ZeroChances_NoRequest()
    {
        var roller = new CustomerRequestRoller(
            [CustomerRequestTestData.Car()],
            CustomerRequestTestData.Table(),
            seed: 0);

        Assert.Null(roller.Roll(
            new ProceduralName("Max", "Weber"), CustomerRequestTestData.Car(), 2026, []));
    }

    [Fact]
    public void SameSeed_SameDraws()
    {
        var table = CustomerRequestTestData.Table(tier3: 1.0);
        var first = new CustomerRequestRoller([CustomerRequestTestData.Car()], table, seed: 7);
        var second = new CustomerRequestRoller([CustomerRequestTestData.Car()], table, seed: 7);
        var customer = new ProceduralName("Max", "Weber");
        CarSpec car = CustomerRequestTestData.Car();

        CustomerRequest? a = first.Roll(customer, car, 2026, []);
        CustomerRequest? b = second.Roll(customer, car, 2026, []);

        Assert.Equal(a?.Quiz?.Text, b?.Quiz?.Text);
        Assert.Equal(a?.Quiz?.Options, b?.Quiz?.Options);
        Assert.Equal(a?.Quiz?.CorrectIndex, b?.Quiz?.CorrectIndex);
    }
}
