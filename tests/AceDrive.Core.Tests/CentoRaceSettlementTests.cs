using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia settle (PORTPLAN 49, Unity
/// CentoMigliaScreen.ProcessReceiptRewards): the purse, the reputation the race
/// grew, the Star of the Show win total and the generated item, then its
/// receipt page. The shell is a fake, so each test sees only the banking.</summary>
public sealed class CentoRaceSettlementTests
{
    private static CentoMigliaEvent Race() =>
        new(1, "Gentlemen Drivers Club", 7, Year: 1924, Week: 3,
            new CentoRoutePlan(
                new AddressLocation("Street 1", 1, HouseSide.Right),
                new AddressLocation("Street 2", 1, HouseSide.Right),
                0.9645));

    private sealed record Harness(
        CentoRaceSettlement Settlement, DriverWallet Wallet, Garage Garage,
        Trunk Trunk, FakeJobSettlementHost Host);

    private static Harness Create(
        double reputation = 1.0, int starWins = 0, CentoRaceRules? rules = null,
        int seed = 1)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var wallet = new DriverWallet(DriverState.Fresh with
        {
            ClubReputation = reputation,
            StarOfTheShowWins = starWins,
        });
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var trunk = new Trunk();
        var settlement = new CentoRaceSettlement(
            wallet, garage, trunk, rules ?? CentoRaceRules.Default,
            new Random(seed), new VoiceBeat(clock), new NullLogger());
        return new Harness(settlement, wallet, garage, trunk, new FakeJobSettlementHost());
    }

    [Fact]
    public void Settle_BanksThePurseAndShowsTheReceipt()
    {
        Harness h = Create(reputation: 1.0);

        h.Settlement.Settle(Race(), 1.0, 1.5, h.Host);

        CentoReceiptScreenViewModel receipt = Assert.IsType<CentoReceiptScreenViewModel>(
            Assert.Single(h.Host.Receipts));
        Assert.Equal("1st PLACE", receipt.PlaceLine);
        Assert.Contains("Placement Earnings", receipt.EarningsLine, StringComparison.Ordinal);
        // The 200 CR first prize scaled by the 1.5 reputation, plus any award.
        // The fixture wallet starts empty, so the purse is the whole balance.
        Assert.True(h.Wallet.Cash >= 300.0);
        Assert.Equal(1.5, h.Wallet.ClubReputation, precision: 6);
        Assert.Single(h.Host.DriverSaves);
        Assert.Equal(1, h.Host.JobRefreshes);
    }

    [Fact]
    public void Continue_OnTheReceipt_LandsOnTheMainPage()
    {
        Harness h = Create();
        h.Settlement.Settle(Race(), 1.0, 1.1, h.Host);
        CentoReceiptScreenViewModel receipt = Assert.IsType<CentoReceiptScreenViewModel>(
            Assert.Single(h.Host.Receipts));

        receipt.ContinueCommand.Execute(null);

        // A race is not a job: the receipt hands the panel back to the menu,
        // never to the jobs list.
        Assert.Equal(1, h.Host.MenuPages);
        Assert.Equal(0, h.Host.ControlPages);
    }

    [Fact]
    public void Settle_NamesTheEventWeekInTheCongratulations()
    {
        Harness h = Create();

        h.Settlement.Settle(Race(), 1.0, 1.1, h.Host);

        CentoReceiptScreenViewModel receipt = Assert.IsType<CentoReceiptScreenViewModel>(
            Assert.Single(h.Host.Receipts));
        Assert.Equal(
            "Congratulations you completed the week 3 Cento Miglia for the Gentlemen Drivers Club",
            receipt.CongratsLine);
    }

    [Fact]
    public void Settle_StoresTheAwardInTheTrunkAndBanksTheWin()
    {
        // A certain award: the logistic saturates for a class far above the
        // pivot, so the roll cannot miss.
        CentoRaceRules rules = CentoRaceRules.Default with
        {
            StarBaseChance = 1.0,
            StarSteepness = 50.0,
            StarClassPivot = 0,
        };
        Harness h = Create(starWins: 2, rules: rules);

        h.Settlement.Settle(Race(), 1.0, 1.1, h.Host);

        Assert.Equal(3, h.Wallet.StarOfTheShowWins);
        TrunkItem item = Assert.Single(h.Trunk.Items);
        Assert.StartsWith("Star of the Show Award 1924", item.Name, StringComparison.Ordinal);
        CentoReceiptScreenViewModel receipt = Assert.IsType<CentoReceiptScreenViewModel>(
            Assert.Single(h.Host.Receipts));
        Assert.True(receipt.StarWon);
        Assert.Contains("STAR OF THE SHOW", receipt.RewardLine, StringComparison.Ordinal);
        // Placement 200 x tier 1 x 1.1 reputation, plus the third win's 1500.
        // The fixture wallet starts empty, so the purse is the whole balance.
        Assert.Equal(220.0 + 1500.0, h.Wallet.Cash, precision: 2);
    }

    [Fact]
    public void Settle_StoresTheCertificateWhenTheAwardIsMissed()
    {
        CentoRaceRules rules = CentoRaceRules.Default with { StarBaseChance = 0.0 };
        Harness h = Create(rules: rules);

        h.Settlement.Settle(Race(), 1.0, 1.1, h.Host);

        TrunkItem item = Assert.Single(h.Trunk.Items);
        Assert.StartsWith("Cento Miglia Certificate Week 3 1924", item.Name, StringComparison.Ordinal);
        Assert.Equal(0, h.Wallet.StarOfTheShowWins);
        CentoReceiptScreenViewModel receipt = Assert.IsType<CentoReceiptScreenViewModel>(
            Assert.Single(h.Host.Receipts));
        Assert.False(receipt.StarWon);
    }

    [Fact]
    public void Settle_LiftsConcentrationForTheWin()
    {
        Harness h = Create();
        double before = h.Wallet.Rating.Concentration;

        h.Settlement.Settle(Race(), 1.0, 1.1, h.Host);

        Assert.Equal(before + 2.0, h.Wallet.Rating.Concentration, precision: 6);
    }
}
