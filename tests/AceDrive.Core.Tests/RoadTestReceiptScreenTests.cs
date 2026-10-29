using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The road test's receipt page (PORTPLAN 49, Unity RoadTestReceiptScreen): one
/// reading per line with the previous issue beside it (except the crowd, which
/// states how many citizens bought La Strada) and its formula underneath, the
/// wallet after the payment and the personal bests. The page only formats what
/// the settle handed it.
/// </summary>
public sealed class RoadTestReceiptScreenTests
{
    private static readonly RoadTestShot Shot =
        new("1990 MAZDA MX-5 NA", "Street 1", 4, OffsetMeters: 0.0, TrackLengthMeters: 20_000.0);

    [Fact]
    public void Receipt_PrintsTheIssueItsFormulasAndThePreviousReadings()
    {
        var wallet = new DriverWallet();
        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            Shot,
            new RoadTestAudienceCounts(Citizens: 1000, Friends: 3, Acquaintances: 2),
            Previous(), RoadTestAudienceRules.Default);
        wallet.AddRoadTest(receipt);
        var page = Create(receipt, wallet);

        Assert.True(page.HasPhotoLocation);
        Assert.Equal("Photo location: Street 1 · No. 4", page.PhotoLine);
        Assert.Equal("1990 MAZDA MX-5 NA", page.CarLine);
        Assert.Equal(
            "40 people from 1000 citizens have bought La Strada.", page.PeopleLine);
        Assert.Equal(
            "friends (3) + acquaintances (2) × 0.5 = 4 / 100 × 1000 citizens = 40 people",
            page.PeopleFormula);
        Assert.Equal("Variety: 100.00% (previously 25.00%)", page.VarietyLine);
        Assert.Equal("first photo — full variety (100%)", page.VarietyFormula);
        Assert.Equal(
            "Car journalist reputation: 100.00 (previously 40.00)", page.JournalistLine);
        Assert.Equal(
            "reputation(2) × test results(50) × √variety = 100.00", page.JournalistFormula);
        Assert.Equal("Magazine price: 50.00 CR (previously 20.00)", page.MagazineLine);
        Assert.Equal("car journalist reputation (100.00) / 2 = 50.00", page.MagazineFormula);
        Assert.Equal("Commission: 200.00 CR", page.CommissionLine);
        Assert.Equal("10% of 40 people buying our magazine at 50.00 CR.", page.CommissionNote);
        Assert.Equal("Forfeit: 400 CR", page.ForfeitLine);
        Assert.Equal("Earnings: 600.00 CR (previously 500.00)", page.EarningsLine);
        Assert.Equal("Cash after: 700.00 CR", page.CashLine);
        Assert.Equal("— best —", page.BestHeader);
        Assert.Equal("most people reached: 40", page.BestPeopleLine);
        Assert.Equal("highest car journalist reputation: 100.00", page.BestJournalistLine);
        Assert.Equal("highest magazine price: 50.00 CR", page.BestMagazineLine);
        Assert.Equal("highest earnings: 900.00", page.BestEarningsLine);

        Assert.Contains("CONTINUE", page.VoiceActions.Keys);
    }

    [Fact]
    public void SecondShoot_PrintsTheVarietyFormulaInsteadOfTheFirstPhotoLine()
    {
        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            Shot,
            RoadTestAudienceCounts.None,
            Previous() with
            {
                HasPhoto = true,
                PhotoStreet = "Street 2",
                PhotoHouseNumber = 7,
                PhotoOffsetMeters = 5_000.0,
            },
            RoadTestAudienceRules.Default);
        var page = Create(receipt, new DriverWallet());

        Assert.Equal(
            "last distance (5.0 km) / max distance in zone (10.0 km) = 50.00%",
            page.VarietyFormula);
        Assert.Equal("Variety: 50.00% (previously 25.00%)", page.VarietyLine);
    }

    [Fact]
    public void ShotWithoutAnAddress_HidesTheLocationLine()
    {
        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            new RoadTestShot("1990 MAZDA MX-5 NA", string.Empty, 0, 0.0, 0.0),
            RoadTestAudienceCounts.None, RoadTestRecord.Empty,
            RoadTestAudienceRules.Default);
        var page = Create(receipt, new DriverWallet());

        Assert.False(page.HasPhotoLocation);
        Assert.Equal("Earnings: 400.00 CR (previously 0.00)", page.EarningsLine);
    }

    [Fact]
    public void Receipt_OptsOutOfTheIdleHide()
    {
        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            Shot, RoadTestAudienceCounts.None, RoadTestRecord.Empty,
            RoadTestAudienceRules.Default);

        Assert.IsAssignableFrom<IKeepPanelOpen>(Create(receipt, new DriverWallet()));
    }

    // The issue before this one: a weaker shoot whose readings the page prints
    // in brackets and whose highs must survive this issue.
    private static RoadTestRecord Previous() => RoadTestRecord.Empty with
    {
        NaturalReputation = 2,
        LastPeopleReached = 10,
        LastVariety = 0.25,
        LastJournalistReputation = 40.0,
        LastMagazinePrice = 20.0,
        LastEarnings = 500.0,
        HighestPeopleReached = 30,
        HighestJournalistReputation = 90.0,
        HighestMagazinePrice = 45.0,
        HighestEarnings = 900.0,
    };

    private static RoadTestReceiptScreenViewModel Create(
        RoadTestReceipt receipt, DriverWallet wallet)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        return new RoadTestReceiptScreenViewModel(
            receipt, wallet, new VoiceBeat(new FakeClock()), new RelayCommand(NoOp));
    }

    private static void NoOp()
    {
    }
}
