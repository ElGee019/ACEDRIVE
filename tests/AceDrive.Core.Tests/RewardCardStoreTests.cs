using System.IO;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// The reward catalogue's file boundary (PORTPLAN 61, Data/reward_cards.json):
/// the shipped file loads into the table the picker draws from — including the
/// word-authored card kinds — and a missing or unusable file degrades to
/// <see cref="RewardTable.Default"/> with a logged warning instead of taking the
/// overlay down.
/// </summary>
public sealed class RewardCardStoreTests
{
    [Fact]
    public void Load_ReadsTheShippedCatalogue()
    {
        string shipped = Path.Combine(
            AppContext.BaseDirectory, "Data", "reward_cards.json");
        var log = new TestLog();

        RewardTable table = new JsonRewardCardStore(shipped, log).Load();

        Assert.Equal(RewardTable.Default.Cards.Count, table.Cards.Count);
        Assert.Equal(RewardTable.Default.DefaultCapacity, table.DefaultCapacity);
        Assert.Equal(RewardTable.Default.CashBonus, table.CashBonus);
        // The word-authored kinds survive the read: a slot card and the cash
        // card are consumed on claim, never stored.
        Assert.Equal(
            RewardCardKind.CashBonus,
            Assert.Single(table.Cards, card => card.Kind == RewardCardKind.CashBonus).Kind);
        Assert.Single(table.Cards, card => card.Kind == RewardCardKind.GloveboxSlot);
        // A use-limited card carries its countdown limit from the file.
        Assert.Equal(5, Assert.Single(table.Cards, card => card.Title == "HEAT-FREE RIDES").Uses);
        // The card effects and their balance amounts are data too: a code
        // switch on the title would silently break a renamed card.
        Assert.Equal(
            RewardCardEffect.DoubleNextTip,
            table.Find("DOUBLE NEXT TIP")!.Effect);
        Assert.Equal(1.2, table.Find(RewardCardEffect.DeliveryProfitBoost)!.Amount);
        Assert.Equal(0.5, table.Find(RewardCardEffect.HalfDamage)!.Amount);
        Assert.Equal(2.0, table.Find(RewardCardEffect.WorkerBonus)!.Amount);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void Load_MissingFileFallsBackToTheDefaults()
    {
        var log = new TestLog();

        RewardTable table = new JsonRewardCardStore(TempFile(), log).Load();

        Assert.Equal(RewardTable.Default, table);
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void Load_UnusableFileFallsBackToTheDefaults()
    {
        // Two cards are the page's minimum: a stripped catalogue cannot fill the
        // picker, so the shipped defaults answer instead of an empty page.
        string file = TempFile();
        try
        {
            File.WriteAllText(file, """{"Cards":[{"Title":"ONE","Description":"only"}]}""");
            var log = new TestLog();

            RewardTable table = new JsonRewardCardStore(file, log).Load();

            Assert.Equal(RewardTable.Default, table);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Load_CatalogueWithoutACardsBlockFallsBackToTheDefaults()
    {
        // A hand-edited file whose Cards block went missing deserializes to a
        // null list; the store must treat that as unusable, never throw.
        string file = TempFile();
        try
        {
            File.WriteAllText(file, """{"DefaultCapacity":2,"CashBonus":500}""");
            var log = new TestLog();

            RewardTable table = new JsonRewardCardStore(file, log).Load();

            Assert.Equal(RewardTable.Default, table);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Load_StoredCardWithoutAnEffect_FallsBackToTheDefaults()
    {
        // A stored card must declare what it does; an effect-less row would sit
        // in the glovebox forever, so the file is treated as unusable.
        string file = TempFile();
        try
        {
            File.WriteAllText(file, """
                {"Cards":[
                  {"Title":"ONE","Description":"one","Kind":"Card"},
                  {"Title":"TWO","Description":"two","Kind":"CashBonus"}]}
                """);
            var log = new TestLog();

            RewardTable table = new JsonRewardCardStore(file, log).Load();

            Assert.Equal(RewardTable.Default, table);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    private static string TempFile() =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".json");
}
