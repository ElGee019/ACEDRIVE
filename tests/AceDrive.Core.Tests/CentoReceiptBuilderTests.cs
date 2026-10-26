using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia receipt (PORTPLAN 49, Unity
/// CentoMigliaScreen.ProcessReceiptRewards): the placement purse, the Star of
/// the Show logistic, the generated award/certificate and the rating award.</summary>
public sealed class CentoReceiptBuilderTests
{
    private static readonly CentoRaceRules Rules = CentoRaceRules.Default;

    private static CentoMigliaEvent Race(int tier = 1, string label = "Gentlemen Drivers Club") =>
        new(tier, label, 7, Year: 1924, Week: 3,
            new CentoRoutePlan(
                new AddressLocation("Street 1", 1, HouseSide.Right),
                new AddressLocation("Street 2", 1, HouseSide.Right),
                0.9645));

    private static CentoReceipt Build(
        int placement = 1, double start = 1.0, double end = 1.0,
        int carClass = 12, int ownedCars = 1, int starWins = 0,
        string carName = "Mazda MX-5", int seed = 0) =>
        CentoReceiptBuilder.Build(
            Race(), placement, start, end, carName, carClass, ownedCars,
            starWins, Rules, new Random(seed));

    [Fact]
    public void PlacementPaysTheLadderTimesTheTierAndTheReputation()
    {
        CentoReceipt first = Build(placement: 1, start: 1.0, end: 2.0);
        Assert.Equal(400.0, first.PlacementEarnings, precision: 6);
        Assert.Equal(1.0, first.EarnedReputation, precision: 6);

        Assert.Equal(150.0, Build(placement: 2).PlacementEarnings, precision: 6);
        // Unity pays nothing past the fourth place.
        Assert.Equal(0.0, Build(placement: 5).PlacementEarnings);
    }

    [Fact]
    public void TotalEarned_IsThePlacementPursePlusAnyPrize()
    {
        CentoReceipt receipt = Build(placement: 1);
        Assert.Equal(
            receipt.PlacementEarnings + receipt.StarPrize,
            receipt.TotalEarned,
            precision: 6);
    }

    [Fact]
    public void StarChance_FollowsTheClassCurveAndThePlace()
    {
        // Unity's logistic at the pivot: half the 0.90 ceiling, scaled 1.2 by
        // the first place.
        Assert.Equal(0.54, CentoReceiptBuilder.StarChance(1, 12, Rules), precision: 6);
        // A very high class clamps to a certain win, a very low one stays rare.
        Assert.Equal(1.0, CentoReceiptBuilder.StarChance(1, 100, Rules), precision: 6);
        Assert.InRange(CentoReceiptBuilder.StarChance(4, 0, Rules), 0.0, 0.02);
    }

    [Fact]
    public void StarWon_PaysPerWinAndNamesTheAward()
    {
        // A class the logistic makes certain wins, so the roll is deterministic
        // whatever the seeded sample says.
        CentoReceipt receipt = Build(placement: 1, carClass: 100, starWins: 2);
        Assert.True(receipt.StarWon);
        Assert.Equal(3, receipt.StarWins);
        Assert.Equal(1500.0, receipt.StarPrize, precision: 6);
        Assert.Equal("Star of the Show Award 1924 - Mazda MX-5", receipt.ItemName);
    }

    [Fact]
    public void StarLost_HandsTheCertificateAndKeepsTheWins()
    {
        // Seed 0's first sample is far above the tiny low-class chance.
        CentoReceipt receipt = Build(placement: 4, carClass: 0, starWins: 2, seed: 0);
        Assert.False(receipt.StarWon);
        Assert.Equal(0.0, receipt.StarPrize);
        Assert.Equal(2, receipt.StarWins);
        Assert.Equal("Cento Miglia Certificate Week 3 1924 - Tier 1", receipt.ItemName);
    }

    [Fact]
    public void ConcentrationAward_FollowsThePlace()
    {
        Assert.Equal(2.0, Build(placement: 1).ConcentrationAward);
        Assert.Equal(1.0, Build(placement: 2).ConcentrationAward);
        Assert.Equal(0.0, Build(placement: 3).ConcentrationAward);
    }

    [Fact]
    public void EarnedReputation_NeverGoesNegative()
    {
        CentoReceipt receipt = Build(start: 2.0, end: 1.5);
        Assert.Equal(0.0, receipt.EarnedReputation);
    }

    [Fact]
    public void SameSeed_AnswersTheSameReceipt()
    {
        CentoReceipt first = Build(seed: 4, carClass: 40);
        CentoReceipt again = Build(seed: 4, carClass: 40);
        Assert.Equal(first, again);
    }
}
