using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia settle (PORTPLAN 49, Unity
/// CentoMigliaScreen.ProcessReceiptRewards): the drawn finishing place and its
/// purse, the reputation the race grew, the Star of the Show win total and the
/// generated item, the final standings, then its receipt page. The shell is a
/// fake, so each test sees only the banking.</summary>
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
        int seed = 1, double assertivity = 0.0, Random? rng = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var wallet = new DriverWallet(DriverState.Fresh with
        {
            ClubReputation = reputation,
            StarOfTheShowWins = starWins,
            Assertivity = assertivity,
        });
        var garage = new Garage(TestRoadCars.Pool);
        garage.Add(TestRoadCars.Pool[0], week: 1);
        var trunk = new Trunk();
        var settlement = new CentoRaceSettlement(
            wallet, garage, trunk, rules ?? CentoRaceRules.Default,
            rng ?? new Random(seed), new VoiceBeat(clock), new NullLogger());
        return new Harness(settlement, wallet, garage, trunk, new FakeJobSettlementHost());
    }

    private static CentoReceiptScreenViewModel Settle(
        Harness h, string playerName = "ElGee", double start = 1.0, double end = 1.1)
    {
        h.Settlement.Settle(Race(), start, end, playerName, h.Host);
        return Assert.IsType<CentoReceiptScreenViewModel>(Assert.Single(h.Host.Receipts));
    }

    // A race with no Star of the Show roll: the purse is the placement ladder
    // alone, so a test can price the drawn place without a prize in the way.
    private static CentoRaceRules NoStar => CentoRaceRules.Default with
    {
        StarBaseChance = 0.0,
    };

    private static double Ladder(CentoReceiptScreenViewModel receipt) =>
        CentoRaceRules.Default.PlacementEarnings[receipt.Placement - 1];

    [Fact]
    public void Settle_DrawsThePlaceAndBanksItsPurse()
    {
        Harness h = Create(rules: NoStar);

        CentoReceiptScreenViewModel receipt = Settle(h, start: 1.0, end: 1.5);

        Assert.InRange(receipt.Placement, 1, 4);
        Assert.Equal(4, receipt.Standings.Count);
        // The 200/150/100/50 ladder scaled by tier 1 and the 1.5 reputation.
        Assert.Equal(Ladder(receipt) * 1.5, h.Wallet.Cash, precision: 2);
        Assert.Equal(1.5, h.Wallet.ClubReputation, precision: 6);
        Assert.Single(h.Host.DriverSaves);
        Assert.Equal(1, h.Host.JobRefreshes);
    }

    [Fact]
    public void Continue_OnTheReceipt_LandsOnTheMainPage()
    {
        Harness h = Create();
        CentoReceiptScreenViewModel receipt = Settle(h);

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

        CentoReceiptScreenViewModel receipt = Settle(h);

        Assert.Equal(
            "Congratulations you completed the week 3 Cento Miglia for the Gentlemen Drivers Club",
            receipt.CongratsLine);
    }

    [Fact]
    public void Settle_ShowsTheThreeRivalsInFinishingOrder()
    {
        Harness h = Create();

        CentoReceiptScreenViewModel receipt = Settle(h, playerName: "ElGee");

        Assert.Equal(4, receipt.Standings.Count);
        // Places are one to four, in order.
        Assert.Equal(
            ["1st", "2nd", "3rd", "4th"],
            receipt.Standings.Select(row => row.Text[..3]).ToArray());
        CentoStandingRow own = Assert.Single(receipt.Standings, row => row.IsPlayer);
        Assert.Contains("ElGee", own.Text, StringComparison.Ordinal);
        string all = string.Join('\n', receipt.Standings.Select(row => row.Text));
        Assert.Contains("Viktor Brandl", all, StringComparison.Ordinal);
        Assert.Contains("Elodie Marchand", all, StringComparison.Ordinal);
        Assert.Contains("Tommy Whitcombe", all, StringComparison.Ordinal);
    }

    [Fact]
    public void Settle_PrintsYouForAnUnnamedPlayer()
    {
        Harness h = Create();

        CentoReceiptScreenViewModel receipt = Settle(h, playerName: string.Empty);

        CentoStandingRow own = Assert.Single(receipt.Standings, row => row.IsPlayer);
        Assert.Contains("YOU", own.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Settle_DrawsEveryPlaceOverSeeds()
    {
        var places = new HashSet<int>();
        for (int seed = 0; seed < 40; seed++)
        {
            Harness h = Create(rules: NoStar, seed: seed);
            places.Add(Settle(h).Placement);
        }

        Assert.Equal([1, 2, 3, 4], places.Order().ToArray());
    }

    [Fact]
    public void Settle_CarriesAssertivityOntoTheReceipt()
    {
        Harness h = Create(assertivity: 3.4);

        CentoReceiptScreenViewModel receipt = Settle(h);

        Assert.Contains("Assertivity(3.4)", receipt.CalculationLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Settle_StoresTheAwardInTheTrunkAndBanksTheWin()
    {
        // A pinned Star of the Show roll (the fake answers its lowest sample),
        // so the drawn place cannot change whether the award is won.
        CentoRaceRules rules = CentoRaceRules.Default with
        {
            StarBaseChance = 1.0,
            StarSteepness = 50.0,
            StarClassPivot = 0,
        };
        Harness h = Create(
            starWins: 2, rules: rules, rng: new FixedRandom(nextDouble: 0.0));

        CentoReceiptScreenViewModel receipt = Settle(h);

        Assert.Equal(3, h.Wallet.StarOfTheShowWins);
        TrunkItem item = Assert.Single(h.Trunk.Items);
        Assert.StartsWith("Star of the Show Award 1924", item.Name, StringComparison.Ordinal);
        Assert.True(receipt.StarWon);
        Assert.Contains("STAR OF THE SHOW", receipt.RewardLine, StringComparison.Ordinal);
        // The drawn place's ladder value x tier 1 x 1.1 reputation, plus the
        // third win's 1500.
        Assert.Equal(Ladder(receipt) * 1.1 + 1500.0, h.Wallet.Cash, precision: 2);
    }

    [Fact]
    public void Settle_StoresTheCertificateWhenTheAwardIsMissed()
    {
        CentoRaceRules rules = CentoRaceRules.Default with { StarBaseChance = 0.0 };
        Harness h = Create(rules: rules);

        CentoReceiptScreenViewModel receipt = Settle(h);

        TrunkItem item = Assert.Single(h.Trunk.Items);
        Assert.StartsWith("Cento Miglia Certificate Week 3 1924", item.Name, StringComparison.Ordinal);
        Assert.Equal(0, h.Wallet.StarOfTheShowWins);
        Assert.False(receipt.StarWon);
    }

    [Fact]
    public void Settle_LiftsConcentrationForThePlace()
    {
        Harness h = Create();
        double before = h.Wallet.Rating.Concentration;

        CentoReceiptScreenViewModel receipt = Settle(h);

        double expected = receipt.Placement switch
        {
            1 => 2.0,
            2 => 1.0,
            _ => 0.0,
        };
        Assert.Equal(before + expected, h.Wallet.Rating.Concentration, precision: 6);
    }
}
