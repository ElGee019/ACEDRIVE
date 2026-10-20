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
    public void FindCurrentIdentity_MostRecentCarLineWins()
    {
        string[] lines =
        [
            "[gameplay] Game Started! ... | ks_mazda_mx5_na | ...",
            "[ui] something else",
            "[physics] Creating car: ks_honda_s2000_ap1",
            "[gameface] Selected Whatever preset_x",
        ];
        Assert.Equal(new CarIdentity("ks_honda_s2000_ap1", null), CarBrand.FindCurrentIdentity(lines));
    }

    [Fact]
    public void FindCurrentIdentity_NoCarLine_IsNull() =>
        Assert.Null(CarBrand.FindCurrentIdentity(["nothing here"]));

    [Fact]
    public void FindCurrentIdentity_EchoKeepsSpawnLinePreset()
    {
        // Real EVO order: the spawn line names the mechanical preset, then
        // the dataUtils echo announces the same car without it — last line
        // alone would strip the variant.
        string[] lines =
        [
            """[gameplay] [info] ACEVO-2629 onSetPlayerCurrentCarCommand: Set new car 4c729a71-d828-0c01-8676-773444b148ac content\cars\ks_abarth_695_biposto\presets\preset_695b_mech_1.mechanicalcarpreset""",
            """[dataUtils] [info] Loaded car 4c729a71d8280c01-8676773444b148ac (ks_abarth_695_biposto)""",
        ];
        var seen = CarBrand.FindCurrentIdentity(lines);
        Assert.Equal("ks_abarth_695_biposto", seen?.KsId);
        Assert.Equal("preset_695b_mech_1", seen?.MechPreset);
    }

    [Fact]
    public void FindCurrentIdentity_OlderCarPresetDoesNotLeak()
    {
        // Newest car announced no preset: a preset from a DIFFERENT, older
        // car must not attach to it.
        string[] lines =
        [
            """[gameplay] onSetPlayerCurrentCarCommand: Set new car 4c72 content\cars\ks_mazda_mx5_na\presets\preset_mx5_mech_1.mechanicalcarpreset""",
            "[physics] Creating car: ks_honda_s2000_ap1",
        ];
        Assert.Equal(new CarIdentity("ks_honda_s2000_ap1", null), CarBrand.FindCurrentIdentity(lines));
    }

    [Theory]
    [InlineData("[physics] Creating car: ks_ferrari_488_gt3_evo", "ferrari")]
    [InlineData("[gameplay] Game Started! ... | ks_datsun_240z_fairlady | ...", "datsun")]
    [InlineData("Loaded car ks_lancia_delta", "lancia")]
    [InlineData("Creating cars: ks_honda_s2000_ap1", null)] // marker must match exactly
    [InlineData("[ui] Selected Whatever preset_x", null)]
    public void BrandOfLogLine(string line, string? expected) =>
        Assert.Equal(expected, CarBrand.BrandOfLogLine(line));

    [Theory]
    [InlineData("[physics] Creating car: ks_ferrari_488_gt3_evo", "ks_ferrari_488_gt3_evo")]
    [InlineData("[gameplay] Game Started! ... | ks_datsun_240z_fairlady | ...", "ks_datsun_240z_fairlady")]
    [InlineData("Loaded car ks_lancia_delta", "ks_lancia_delta")]
    [InlineData("[ui] Selected Whatever preset_x", null)]
    public void KsIdOfLogLine(string line, string? expected) =>
        Assert.Equal(expected, CarBrand.KsIdOfLogLine(line));

    [Fact]
    public void IdentityOfLogLine_SpawnPathCarriesPreset()
    {
        // The M2 line as the game writes it: folder id + mechanical preset.
        string line = """[gameplay] [info] ACEVO-2629 onSetPlayerCurrentCarCommand: Set new car 47cb0b33-34f2-3c54-3e28-9531c84e0794 content\cars\ks_bmw_m2_coupe\presets\preset_m2_mech_1.mechanicalcarpreset""";
        Assert.Equal(new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mech_1"), CarBrand.IdentityOfLogLine(line));
    }

    [Theory]
    [InlineData("[physics] Creating car: ks_ferrari_488_gt3_evo", "ks_ferrari_488_gt3_evo")]
    [InlineData("[ui] Selected BMW M2 Coupe preset_m2_mech_1 preset_m2_visual_1", null)]
    public void IdentityOfLogLine_ShortOrShowroomLines(string line, string? expectedId)
    {
        var seen = CarBrand.IdentityOfLogLine(line);
        Assert.Equal(expectedId, seen?.KsId);
        Assert.Null(seen?.MechPreset);
    }
}
