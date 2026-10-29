using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The road test's receipt math (PORTPLAN item 49, Unity PopulateReceipt): the
/// crowd, the variety against the last shoot, the car journalist's reputation,
/// the magazine price and the earnings, folded with the previous issue and the
/// personal bests. Everything here is data; the page formats the sentences.
/// </summary>
public sealed class RoadTestReceiptBuilderTests
{
    private static RoadTestShot Shot(double offset = 0.0, double length = 20_000.0) =>
        new("1990 MAZDA MX-5 NA", "Street 1", 4, offset, length);

    [Fact]
    public void FirstPhoto_PaysFullVarietyAndTheForfeit()
    {
        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            Shot(),
            new RoadTestAudienceCounts(Citizens: 1000, Friends: 3, Acquaintances: 2),
            RoadTestRecord.Empty with { NaturalReputation = 2 },
            RoadTestAudienceRules.Default);

        Assert.Equal(40, receipt.PeopleReached);
        Assert.Equal(0, receipt.PreviousPeopleReached);
        Assert.True(receipt.FirstPhoto);
        Assert.Equal(1.0, receipt.Variety);
        Assert.Equal(50, receipt.ResultReputation);
        Assert.Equal(100.0, receipt.JournalistReputation, precision: 6);
        Assert.Equal(50.0, receipt.MagazinePrice, precision: 6);
        Assert.Equal(200.0, receipt.Commission, precision: 6);
        Assert.Equal(600.0, receipt.Earnings, precision: 6);
        Assert.Equal(600.0, receipt.HighestEarnings, precision: 6);
        Assert.Equal(40, receipt.HighestPeopleReached);
        Assert.True(receipt.HasPhotoLocation);
        // The address and the lap offset are what the next shoot measures from.
        Assert.Equal("Street 1", receipt.PhotoStreet);
        Assert.Equal(4, receipt.PhotoHouseNumber);
        Assert.Equal(0.0, receipt.PhotoOffsetMeters);
    }

    [Fact]
    public void SecondPhoto_PricesTheVarietyAgainstTheLastShoot()
    {
        RoadTestRecord previous = RoadTestRecord.Empty with
        {
            NaturalReputation = 3,
            HasPhoto = true,
            PhotoStreet = "Street 2",
            PhotoHouseNumber = 7,
            PhotoOffsetMeters = 5_000.0,
            LastPeopleReached = 40,
            LastVariety = 0.8,
            LastJournalistReputation = 150.0,
            LastMagazinePrice = 75.0,
            LastEarnings = 700.0,
            HighestPeopleReached = 40,
            HighestJournalistReputation = 150.0,
            HighestMagazinePrice = 75.0,
            HighestEarnings = 700.0,
        };

        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            Shot(offset: 0.0),
            new RoadTestAudienceCounts(Citizens: 1000, Friends: 3, Acquaintances: 2),
            previous,
            RoadTestAudienceRules.Default);

        // 5 km apart on a 20 km lap: half the lap is the zone's widest span.
        Assert.Equal(5.0, receipt.DistanceKm, precision: 6);
        Assert.Equal(10.0, receipt.MaxDistanceKm, precision: 6);
        Assert.Equal(0.5, receipt.Variety, precision: 6);
        Assert.False(receipt.FirstPhoto);
        Assert.Equal(3 * 50 * Math.Sqrt(0.5), receipt.JournalistReputation, precision: 6);
        Assert.Equal(40, receipt.PreviousPeopleReached);
        Assert.Equal(0.8, receipt.PreviousVariety, precision: 6);
        Assert.Equal(700.0, receipt.PreviousEarnings, precision: 6);
        // A weaker issue never lowers a personal best.
        Assert.Equal(40, receipt.HighestPeopleReached);
        Assert.Equal(150.0, receipt.HighestJournalistReputation, precision: 6);
        Assert.Equal(700.0, receipt.HighestEarnings, precision: 6);
    }

    [Fact]
    public void SameSpot_KeepsUnitysVarietyFloor()
    {
        RoadTestRecord previous = RoadTestRecord.Empty with
        {
            HasPhoto = true,
            PhotoStreet = "Street 1",
            PhotoHouseNumber = 4,
            PhotoOffsetMeters = 0.0,
        };

        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            Shot(offset: 0.0), RoadTestAudienceCounts.None, previous,
            RoadTestAudienceRules.Default);

        Assert.Equal(0.001, receipt.Variety, precision: 6);
        Assert.Equal(0.0, receipt.DistanceKm);
    }

    [Fact]
    public void ShotWithoutAnAddress_KeepsTheReadingsButPrintsNoLocation()
    {
        var shot = new RoadTestShot("1990 MAZDA MX-5 NA", string.Empty, 0, 0.0, 0.0);

        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            shot, RoadTestAudienceCounts.None, RoadTestRecord.Empty,
            RoadTestAudienceRules.Default);

        Assert.False(receipt.HasPhotoLocation);
        Assert.Equal(string.Empty, receipt.PhotoStreet);
        Assert.Equal(0, receipt.PhotoHouseNumber);
        // No lap length either: the first-photo answer, not a divide by zero.
        Assert.True(receipt.FirstPhoto);
        Assert.Equal(1.0, receipt.Variety);
        Assert.Equal(RoadTestAudienceRules.Default.BaseForfeit, receipt.Earnings, precision: 6);
    }

    [Fact]
    public void ToRecord_LeavesTheIssueAndItsBestsForTheNextOne()
    {
        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            Shot(offset: 1_500.0),
            new RoadTestAudienceCounts(Citizens: 1000, Friends: 3, Acquaintances: 2),
            RoadTestRecord.Empty with { NaturalReputation = 4 },
            RoadTestAudienceRules.Default);

        RoadTestRecord record = receipt.ToRecord();

        Assert.Equal(4, record.NaturalReputation);
        Assert.True(record.HasPhoto);
        Assert.True(record.HasPreviousPhoto);
        Assert.Equal("Street 1", record.PhotoStreet);
        Assert.Equal(4, record.PhotoHouseNumber);
        Assert.Equal(1_500.0, record.PhotoOffsetMeters, precision: 6);
        Assert.Equal(receipt.PeopleReached, record.LastPeopleReached);
        Assert.Equal(receipt.Variety, record.LastVariety, precision: 6);
        Assert.Equal(receipt.JournalistReputation, record.LastJournalistReputation, precision: 6);
        Assert.Equal(receipt.MagazinePrice, record.LastMagazinePrice, precision: 6);
        Assert.Equal(receipt.Earnings, record.LastEarnings, precision: 6);
        Assert.Equal(receipt.HighestEarnings, record.HighestEarnings, precision: 6);
    }

    [Fact]
    public void SameInputs_AnswerTheSameReceipt()
    {
        var audience = new RoadTestAudienceCounts(500, 2, 1);
        RoadTestRecord previous = RoadTestRecord.Empty with { NaturalReputation = 7 };

        RoadTestReceipt first = RoadTestReceiptBuilder.Build(Shot(), audience, previous, RoadTestAudienceRules.Default);
        RoadTestReceipt again = RoadTestReceiptBuilder.Build(Shot(), audience, previous, RoadTestAudienceRules.Default);

        Assert.Equal(first, again);
    }
}
