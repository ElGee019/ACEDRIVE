using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The all-players car-km board behind the dynamic XP rarity.</summary>
public sealed class CarRarityTableTests
{
    [Fact]
    public void Empty_AnswersRarityOne()
    {
        CarRarityTable table = CarRarityTable.Empty;
        Assert.Equal(0, table.CarCount);
        Assert.Equal(1, table.Rarity("ks_unknown"));
        Assert.Equal(1, table.Rarity(""));
        Assert.Equal(0, table.Km("ks_unknown"));
    }

    [Fact]
    public void RanksMostDrivenFirst()
    {
        CarRarityTable table = CarRarityTable.From(
        [
            new CarKm("b", 100),
            new CarKm("a", 300),
            new CarKm("c", 50),
        ]);

        Assert.Equal(3, table.CarCount);
        Assert.Equal(1, table.Rarity("a"));
        Assert.Equal(2, table.Rarity("b"));
        Assert.Equal(3, table.Rarity("c"));
        Assert.Equal(300, table.Km("a"));
    }

    [Fact]
    public void EqualKm_SharesTheRank()
    {
        CarRarityTable table = CarRarityTable.From(
        [
            new CarKm("a", 100),
            new CarKm("b", 100),
            new CarKm("c", 10),
        ]);

        Assert.Equal(1, table.Rarity("a"));
        Assert.Equal(1, table.Rarity("b"));
        Assert.Equal(2, table.Rarity("c"));
    }

    [Fact]
    public void CarMissingFromTheBoard_IsTheRarest()
    {
        CarRarityTable table = CarRarityTable.From([new CarKm("a", 100), new CarKm("b", 60)]);
        // Unity GetRarity: an unlisted car has never been driven, so it sits
        // one place behind the last ranked car.
        Assert.Equal(3, table.Rarity("ks_new"));
    }

    [Fact]
    public void EmptyOrNegativeRows_AreIgnored()
    {
        CarRarityTable table = CarRarityTable.From(
        [
            new CarKm("", 100),
            new CarKm("a", -1),
            new CarKm("b", 10),
        ]);

        Assert.Equal(1, table.CarCount);
        Assert.Equal(1, table.Rarity("b"));
    }
}
