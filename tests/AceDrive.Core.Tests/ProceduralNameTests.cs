using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The passenger draw replays Unity's NamePoolManager rules for the
/// Germany cultural zone: weighted roulette over the zone's pools, then
/// first name, surname and companion suffix. Seeded, so every sequence is
/// deterministic (the same discipline as RoadTestLedger's draw).</summary>
public sealed class ProceduralNameTests
{
    // A and B share no name, so a draw's origin pool is always readable
    // from the result. B's weight is a thousandth of A's: A dominates.
    private static readonly NamePoolTable TwoPools = new(
    [
        new NamePool("a", 1000, ["Anna", "Aribert"], ["Abt", "Amsel"]),
        new NamePool("b", 1, ["Bert", "Berta"], ["Bach", "Baumann"]),
    ],
    ["and some friends", "and some colleagues"]);

    [Fact]
    public void SameSeed_ReplaysSameSequence()
    {
        var first = new ProceduralNameGenerator(TwoPools, seed: 7);
        var second = new ProceduralNameGenerator(TwoPools, seed: 7);
        foreach (int _ in Enumerable.Range(0, 20))
        {
            Assert.Equal(first.Next(), second.Next());
        }
    }

    // 1000:1 weights keep B rare but reachable; the part lists below mirror
    // TwoPools so membership proves the draw never leaves the table.
    private static readonly string[] FirstNames = ["Anna", "Aribert", "Bert", "Berta"];
    private static readonly string[] Surnames = ["Abt", "Amsel", "Bach", "Baumann"];
    private static readonly string[] Suffixes = ["and some friends", "and some colleagues"];

    [Fact]
    public void Draws_ComeFromTheTableParts()
    {
        var names = new ProceduralNameGenerator(TwoPools, seed: 3);
        foreach (int _ in Enumerable.Range(0, 50))
        {
            ProceduralName drawn = names.Next();
            Assert.Contains(drawn.FirstName, FirstNames);
            Assert.Contains(drawn.Surname, Surnames);
            Assert.Contains(drawn.Suffix, Suffixes);
        }
    }

    [Fact]
    public void Weights_ShapeThePoolMix()
    {
        // 1000:1 roulette: the light pool may appear, the heavy one must
        // dominate. Fixed seed keeps the mix itself deterministic.
        var names = new ProceduralNameGenerator(TwoPools, seed: 11);
        int heavy = 0;
        foreach (int _ in Enumerable.Range(0, 200))
        {
            ProceduralName drawn = names.Next();
            if (drawn.FirstName.StartsWith('A')) heavy++;
        }

        Assert.True(heavy >= 190, $"expected pool a to dominate, got {heavy}/200");
    }

    [Fact]
    public void Full_CarriesTheCompanionSuffix_LikeUnity()
    {
        var withGroup = new ProceduralName("Klaus", "Berger", "and some friends");
        Assert.Equal("Klaus Berger and some friends", withGroup.Full);

        // No suffix (a solo passenger) never leaves a dangling space.
        Assert.Equal("Klaus Berger", new ProceduralName("Klaus", "Berger").Full);
    }

    [Fact]
    public void ForCapacity_DropsTheGroupOnASingleSeat()
    {
        var withGroup = new ProceduralName("Klaus", "Berger", "and some friends");

        // One seat, one customer: no companions with nowhere to sit.
        ProceduralName solo = withGroup.ForCapacity(1);
        Assert.Empty(solo.Suffix);
        Assert.Equal("Klaus Berger", solo.Full);
        // The source identity is untouched, so a later group ride still names it.
        Assert.Equal("and some friends", withGroup.Suffix);

        Assert.Equal(withGroup, withGroup.ForCapacity(2));
        Assert.Equal(withGroup, withGroup.ForCapacity(4));
    }
}
