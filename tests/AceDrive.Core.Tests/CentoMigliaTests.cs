using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia rules (PORTPLAN 49, Unity CentoMigliaScreen):
/// the era decade, the reputation floor and the tier gates.</summary>
public sealed class CentoMigliaTests
{
    private static CentoTier Tier(string id, int required) =>
        new(id, "Label", "Description", required);

    [Theory]
    [InlineData(1924, 1920)]
    [InlineData(1929, 1920)]
    [InlineData(1930, 1930)]
    [InlineData(2001, 2000)]
    public void EraDecade_RoundsDownToTheDecade(int year, int expected) =>
        Assert.Equal(expected, CentoMiglia.EraDecade(year));

    [Fact]
    public void DisplayReputation_FloorsAFreshDriverToOne()
    {
        // Unity's header keeps a first-ever driver's raw 0 readable.
        Assert.Equal(1.0, CentoMiglia.DisplayReputation(0.0));
        Assert.Equal(1.0, CentoMiglia.DisplayReputation(-3.0));
        Assert.Equal(2.5, CentoMiglia.DisplayReputation(2.5));
    }

    [Fact]
    public void Eligibility_FreeTierIgnoresReputation()
    {
        CentoTier gentlemen = Tier(CentoMiglia.GentlemenDrivers, 1);
        Assert.True(CentoMiglia.IsTierEligible(gentlemen, 0.0, hasDiscoveredStreets: true));
    }

    [Fact]
    public void Eligibility_LockedUntilTheRequiredLevel()
    {
        CentoTier era = Tier("era_event", 2);
        Assert.False(CentoMiglia.IsTierEligible(era, 1.99, hasDiscoveredStreets: true));
        Assert.True(CentoMiglia.IsTierEligible(era, 2.0, hasDiscoveredStreets: true));
    }

    [Fact]
    public void Eligibility_NoDiscoveredStreetLocksEveryTier()
    {
        Assert.False(CentoMiglia.IsTierEligible(
            Tier(CentoMiglia.GentlemenDrivers, 1), 5.0, hasDiscoveredStreets: false));
        Assert.False(CentoMiglia.IsTierEligible(
            Tier("event8", 128), 500.0, hasDiscoveredStreets: false));
    }

    [Fact]
    public void Wallet_CarriesTheClubReputation()
    {
        // The page prints what the wallet holds: a save from before the field
        // existed reads Unity's raw 0, a raced one keeps its level.
        Assert.Equal(0.0, new DriverWallet().ClubReputation);
        var saved = DriverState.Fresh with { ClubReputation = 2.5 };
        var wallet = new DriverWallet(saved);
        Assert.Equal(2.5, wallet.ClubReputation);
        Assert.Equal(2.5, wallet.Snapshot().ClubReputation);
    }
}
