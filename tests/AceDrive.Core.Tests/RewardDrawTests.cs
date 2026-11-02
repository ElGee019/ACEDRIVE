using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The care-package picker's draw (PORTPLAN 61, Unity
/// TodoRewardsScreenController.RandomizeButtons' two distinct rewards): two
/// different cards from the catalogue, deterministic for a seeded stream, and no
/// page at all when the catalogue cannot fill one.
/// </summary>
public sealed class RewardDrawTests
{
    private static readonly RewardTable Table = new(
        [
            new RewardCard("DOUBLE NEXT TIP", "Your next customer tip will be doubled"),
            new RewardCard("NEXT FINE WAIVED", "Your next fine will be waived"),
            new RewardCard("HEAT-FREE RIDES", "No police the next 5 rides"),
        ]);

    [Fact]
    public void Offer_ReturnsTwoDifferentCardsFromTheCatalogue()
    {
        for (int seed = 0; seed < 50; seed++)
        {
            var offer = Assert.IsType<RewardOffer>(RewardDraw.Offer(Table, new Random(seed)));

            Assert.NotEqual(offer.First.Title, offer.Second.Title);
            Assert.Contains(offer.First, Table.Cards);
            Assert.Contains(offer.Second, Table.Cards);
        }
    }

    [Fact]
    public void Offer_IsDeterministicForASeed()
    {
        RewardOffer? first = RewardDraw.Offer(Table, new Random(7));
        RewardOffer? second = RewardDraw.Offer(Table, new Random(7));

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Offer_IsNullWhenTheCatalogueCannotFillAPage(int cardCount)
    {
        var table = new RewardTable(
            [.. Table.Cards.Take(cardCount)]);

        Assert.Null(RewardDraw.Offer(table, new Random(1)));
    }
}
