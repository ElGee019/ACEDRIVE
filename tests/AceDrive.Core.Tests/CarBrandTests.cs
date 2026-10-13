using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Car-id -> brand mapping + ACE log-line scan, ported from carwatch tests.</summary>
public sealed class CarBrandTests
{
    [Theory]
    [InlineData("ks_mazda_mx5_na", "mazda")]
    [InlineData("ks_alfa_romeo_75_turbo_evo", "alfa romeo")]
    [InlineData("ks_mercedes_amg_gt2", "mercedes")]
    [InlineData("ks_mercedes_190e_evo_ii", "mercedes")]
    [InlineData("ks_porsche_992_gt3_rs", "porsche")]
    public void BrandOf_LongestPrefixMatch(string ksId, string expected) =>
        Assert.Equal(expected, CarBrand.BrandOf(ksId));

    [Fact]
    public void BrandOf_Unknown_IsNull() =>
        Assert.Null(CarBrand.BrandOf("ks_unknown_xyz"));

    [Fact]
    public void FindCurrentKs_MostRecentCarLineWins()
    {
        string[] lines =
        [
            "[gameplay] Game Started! ... | ks_mazda_mx5_na | ...",
            "[ui] something else",
            "[physics] Creating car: ks_honda_s2000_ap1",
            "[gameface] Selected Whatever preset_x",
        ];
        Assert.Equal("ks_honda_s2000_ap1", CarBrand.FindCurrentKs(lines));
    }

    [Fact]
    public void FindCurrentKs_NoCarLine_IsNull() =>
        Assert.Null(CarBrand.FindCurrentKs(["nothing here"]));

    [Theory]
    [InlineData("[physics] Creating car: ks_ferrari_488_gt3_evo", "ferrari")]
    [InlineData("[gameplay] Game Started! ... | ks_datsun_240z_fairlady | ...", "datsun")]
    [InlineData("Loaded car ks_lancia_delta", "lancia")]
    [InlineData("Creating cars: ks_honda_s2000_ap1", null)] // marker must match exactly
    [InlineData("[ui] Selected Whatever preset_x", null)]
    public void BrandOfLogLine(string line, string? expected) =>
        Assert.Equal(expected, CarBrand.BrandOfLogLine(line));
}
