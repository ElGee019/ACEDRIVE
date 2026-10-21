using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The proposal page's radius field commits with Unity's
/// OnRadiusEndEdit rules: empty clears, junk restores, the 5 km floor snaps,
/// fractions round.</summary>
public sealed class TaxiJobRadiusTests
{
    [Fact]
    public void Empty_ClearsToNoLimit()
    {
        Assert.Equal(0, TaxiJobRadius.Normalize("", 12));
        Assert.Equal(0, TaxiJobRadius.Normalize("   ", 12));
        Assert.Equal(0, TaxiJobRadius.Normalize(null, 12));
        Assert.Equal(0, TaxiJobRadius.Normalize("0", 12));
        Assert.Equal(0, TaxiJobRadius.Normalize("0.0", 12));
    }

    [Fact]
    public void JunkOrNegative_KeepsLastCommitted()
    {
        Assert.Equal(12, TaxiJobRadius.Normalize("eight", 12));
        Assert.Equal(12, TaxiJobRadius.Normalize("-4", 12));
        Assert.Equal(12, TaxiJobRadius.Normalize("12x", 12));
        Assert.Equal(0, TaxiJobRadius.Normalize("??", 0));
    }

    [Fact]
    public void Floor_SnapsSmallRadiiToMinimum()
    {
        Assert.Equal(TaxiJobRadius.MinimumKm, TaxiJobRadius.Normalize("1", 0));
        Assert.Equal(TaxiJobRadius.MinimumKm, TaxiJobRadius.Normalize("4.9", 30));
        Assert.Equal(TaxiJobRadius.MinimumKm, TaxiJobRadius.Normalize("4,9", 30));
    }

    [Fact]
    public void Fractions_RoundToWholeKm()
    {
        Assert.Equal(12, TaxiJobRadius.Normalize("12.4", 0));
        Assert.Equal(13, TaxiJobRadius.Normalize("12.6", 0));
        Assert.Equal(13, TaxiJobRadius.Normalize("12.5", 0)); // Unity rounds halves away from zero
        Assert.Equal(12, TaxiJobRadius.Normalize("12,4", 0));
    }

    [Fact]
    public void Effective_NormalizesTheLimitUsedByTheDraw()
    {
        // Unity's EffectiveRadiusKm: negative means unlimited, positive
        // values below the floor snap to it, 0 stays unlimited.
        Assert.Equal(0, TaxiJobRadius.Effective(-4));
        Assert.Equal(0, TaxiJobRadius.Effective(0));
        Assert.Equal(TaxiJobRadius.MinimumKm, TaxiJobRadius.Effective(1));
        Assert.Equal(TaxiJobRadius.MinimumKm, TaxiJobRadius.Effective(4));
        Assert.Equal(TaxiJobRadius.MinimumKm, TaxiJobRadius.Effective(5));
        Assert.Equal(12, TaxiJobRadius.Effective(12));
    }
}
