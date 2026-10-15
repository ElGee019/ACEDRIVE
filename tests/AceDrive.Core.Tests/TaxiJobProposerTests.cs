using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>One proposal draw = passenger + second-address resident + the
/// pickup coin flip, all from the seeded stream so a whole sequence replays
/// (Unity TaxiJobGenerator steps 3/4/6).</summary>
public sealed class TaxiJobProposerTests
{
    // Two pools with disjoint names, so every draw's origin is readable.
    private static readonly NamePoolTable Table = new(
    [
        new NamePool("a", 1.0, ["Anna", "Aribert"], ["Abt", "Amsel"]),
        new NamePool("b", 1.0, ["Bert", "Berta"], ["Bach", "Baumann"]),
    ],
    ["alone", "together"]);

    [Fact]
    public void SameSeed_ReplaysWholeProposals()
    {
        var first = new TaxiJobProposer(Table, seed: 9);
        var second = new TaxiJobProposer(Table, seed: 9);
        foreach (int _ in Enumerable.Range(0, 12))
        {
            Assert.Equal(first.Next(), second.Next());
        }
    }

    private static readonly string[] FirstNames = ["Anna", "Aribert", "Bert", "Berta"];
    private static readonly string[] Surnames = ["Abt", "Amsel", "Bach", "Baumann"];
    private static readonly string[] Suffixes = ["alone", "together"];

    [Fact]
    public void Next_PairsTwoResidents_AndFlipsTheLegs()
    {
        var proposer = new TaxiJobProposer(Table, seed: 4);
        var flips = new HashSet<bool>();
        foreach (int _ in Enumerable.Range(0, 60))
        {
            TaxiProposal proposal = proposer.Next();
            // Both residents come from the table's parts (any pool), and the
            // passenger keeps its companion suffix for the identity line.
            Assert.Contains(proposal.Customer.FirstName, FirstNames);
            Assert.Contains(proposal.SecondResident.Surname, Surnames);
            Assert.Contains(proposal.Customer.Suffix, Suffixes);
            flips.Add(proposal.HomeIsPickup);
        }

        // Unity rolls the coin per job (Random.value < 0.5): 60 draws must
        // have shown both orders, or the flip is stuck on one leg.
        Assert.Equal(2, flips.Count);
    }
}
