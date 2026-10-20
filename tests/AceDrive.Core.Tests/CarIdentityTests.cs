using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Identity -> pool-row resolution (PORTPLAN 62): exact variant
/// first, then the folder (null-preset) row, then the lone row; never a
/// guess between known siblings. Refined keeps presets across echoes.</summary>
public sealed class CarIdentityTests
{
    private static readonly CarSpec Base =
        new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe");
    private static readonly CarSpec Mech =
        new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe", Passengers: 4) { Preset = "preset_m2_mech_1" };
    private static readonly CarSpec Perf =
        new("ks_bmw_m2_coupe", 2023, "BMW", "m2 g87 coupe m performance", Passengers: 2) { Preset = "preset_m2_mperf_1" };

    [Fact]
    public void FindIn_ExactPreset_WinsOverFolderRow()
    {
        var pool = new List<CarSpec> { Base, Mech, Perf };
        Assert.Equal(Perf, new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mperf_1").FindIn(pool));
    }

    [Fact]
    public void FindIn_UnknownOrMissingPreset_LandsOnFolderRow()
    {
        var pool = new List<CarSpec> { Base, Perf };
        Assert.Equal(Base, new CarIdentity("ks_bmw_m2_coupe", "preset_from_the_future").FindIn(pool));
        Assert.Equal(Base, new CarIdentity("ks_bmw_m2_coupe", null).FindIn(pool));
    }

    [Fact]
    public void FindIn_AmbiguousVariants_ResolvesNothing()
    {
        // Data holds two variants and no folder row; a preset-less line must
        // not silently key one variant's specs to the other.
        var pool = new List<CarSpec> { Mech, Perf };
        Assert.Null(new CarIdentity("ks_bmw_m2_coupe", null).FindIn(pool));
    }

    [Fact]
    public void FindIn_LoneVariantRow_IsTheAnswer()
    {
        // Pool only knows one preset so far: whatever else the game announces
        // for that folder, this is the best-known row.
        var pool = new List<CarSpec> { Perf };
        Assert.Equal(Perf, new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mech_1").FindIn(pool));
        Assert.Equal(Perf, new CarIdentity("ks_bmw_m2_coupe", null).FindIn(pool));
    }

    [Fact]
    public void FindIn_OtherFolder_ResolvesNothing() =>
        Assert.Null(new CarIdentity("ks_mazda_mx5_na", null).FindIn([Base, Perf]));

    [Fact]
    public void RefinedBy_EchoKeepsPreset_NewSpawnReplacesIt()
    {
        var spawned = new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mech_1");

        // Same car's preset-less "Loaded car" echo: the variant survives.
        Assert.Equal(spawned, spawned.RefinedBy(new CarIdentity("ks_bmw_m2_coupe", null)));

        // A new spawn (same folder, other variant) replaces; other car too.
        Assert.Equal(
            new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mperf_1"),
            spawned.RefinedBy(new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mperf_1")));
        Assert.Equal(
            new CarIdentity("ks_mazda_mx5_na", null),
            spawned.RefinedBy(new CarIdentity("ks_mazda_mx5_na", null)));
    }

    [Fact]
    public void Matches_BaseRow_AcceptsAnyPresetOfTheFolder()
    {
        var announced = new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mech_1");

        // Grandma's pick and the dealer can own the folder row while the game
        // spawns a showroom variant of it.
        Assert.True(announced.Matches(Base.Key));
    }

    [Fact]
    public void Matches_AnnouncedPreset_RequiresThatExactVariantRow()
    {
        var announced = new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mech_1");

        Assert.True(announced.Matches(Mech.Key));
        Assert.False(announced.Matches(Perf.Key));
    }

    [Fact]
    public void Matches_PresetlessAnnouncement_AcceptsEveryRowInTheFolder()
    {
        var announced = new CarIdentity("ks_bmw_m2_coupe", null);

        Assert.True(announced.Matches(Base.Key));
        Assert.True(announced.Matches(Mech.Key));
        Assert.True(announced.Matches(Perf.Key));
    }

    [Fact]
    public void Matches_OtherFolderOrMalformedKey_IsFalse()
    {
        var announced = new CarIdentity("ks_bmw_m2_coupe", null);

        Assert.False(announced.Matches("ks_mazda_mx5_na"));
        Assert.False(announced.Matches("ks_bmw_m2_coupe_extra"));
        Assert.False(announced.Matches(""));
    }
}
