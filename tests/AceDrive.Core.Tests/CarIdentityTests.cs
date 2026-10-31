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

    private static readonly List<CarSpec> Folder = [Base, Mech, Perf];

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
    public void Matches_BaseRow_AcceptsAnUnmodelledPresetOfTheFolder()
    {
        // The dealer can own the folder row while the game spawns a showroom
        // variant the catalogue has not modelled yet.
        var announced = new CarIdentity("ks_bmw_m2_coupe", "preset_from_the_future");

        Assert.True(announced.Matches(Base, Folder));
    }

    [Fact]
    public void Matches_BaseRow_RejectsAKnownSiblingVariant()
    {
        // A variant row in the pool makes the folder row a distinct car:
        // driving the sibling must not count as the base car.
        var announced = new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mech_1");

        Assert.False(announced.Matches(Base, Folder));
    }

    [Fact]
    public void Matches_StandardAndWeissachPackage_AreDifferentCars()
    {
        var standard = new CarSpec("ks_porsche_992_gt3_rs", 2021, "Porsche", "992 gt3 rs");
        var weissach = standard with
        {
            Model = "992 gt3 rs weissach",
            Preset = "preset_gt3rs_mech_2",
        };
        var pool = new List<CarSpec> { standard, weissach };
        var announced = new CarIdentity(
            "ks_porsche_992_gt3_rs", "preset_gt3rs_mech_2");

        Assert.True(announced.Matches(weissach, pool));
        Assert.False(announced.Matches(standard, pool));
    }

    [Fact]
    public void Matches_AnnouncedPreset_RequiresThatExactVariantRow()
    {
        var announced = new CarIdentity("ks_bmw_m2_coupe", "preset_m2_mech_1");

        Assert.True(announced.Matches(Mech, Folder));
        Assert.False(announced.Matches(Perf, Folder));
    }

    [Fact]
    public void Matches_PresetlessAnnouncement_AcceptsEveryRowInTheFolder()
    {
        var announced = new CarIdentity("ks_bmw_m2_coupe", null);

        Assert.True(announced.Matches(Base, Folder));
        Assert.True(announced.Matches(Mech, Folder));
        Assert.True(announced.Matches(Perf, Folder));
    }

    [Fact]
    public void Matches_OtherFolderOrMalformedId_IsFalse()
    {
        var announced = new CarIdentity("ks_bmw_m2_coupe", null);
        var other = new CarSpec("ks_mazda_mx5_na", 1990, "Mazda", "mx5 na");

        Assert.False(announced.Matches(other, Folder));
        Assert.False(announced.Matches(Base with { Id = "ks_bmw_m2_coupe_extra" }, Folder));
        Assert.False(announced.Matches(Base with { Id = "" }, Folder));
    }
}
