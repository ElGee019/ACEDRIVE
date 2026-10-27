using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The operating-zone growth on its own (PORTPLAN 58): the introduction gate,
/// the threshold curve, the street the map names and the world push the settle
/// hook triggers. The coordinator is not involved, so each test sees only this
/// collaborator's decisions: which gate held, which street joined, what the
/// report says and what was pushed.
/// </summary>
public sealed class ZoneGrowthTests
{
    private const string Driver = "ElGee";

    // The shipped pool continues past the base pair; the growth tests that
    // claim a third street need that row even though AddressMap.Default (the
    // deterministic fallback) stays at two.
    private static readonly AddressMap ThreeStreets = AddressMap.Default with
    {
        Streets =
        [
            .. AddressMap.Default.Streets,
            new AddressStreet("street-3", "Street 3", 0.0, 0.5, Fictional: true),
        ],
    };

    private sealed record Fixture(
        ZoneGrowth Growth, OperatingZone Zone, FakePlayerTelemetry Online, TestLog Log);

    public ZoneGrowthTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static Fixture Build(
        bool onboardingDone, int expansionJobs = 0, int expansions = 0,
        string? displayName = Driver, int streets = 1, AddressMap? map = null)
    {
        map ??= AddressMap.Default;
        FakeProfileStore profiles = displayName is null
            ? new FakeProfileStore()
            : new FakeProfileStore(
                new PlayerProfile(displayName, "Germany", "Brands", "street-1", 1));
        var saved = new DriverState(
            0.0, 0.0,
            ExpansionJobs: expansionJobs, ExpansionCount: expansions,
            StreetIds: LegacyStreets(streets));
        var zone = new OperatingZone(
            saved, seed: 0, map,
            homeStreetId: profiles.Load()?.StreetId,
            initialKm: AddressBook.MapLengthKm(map));
        var onboarding = new FakeOnboardingStore();
        onboarding.Save(OnboardingState.Fresh with { BirthdayDone = onboardingDone });
        var online = new FakePlayerTelemetry();
        var log = new TestLog();
        var growth = new ZoneGrowth(zone, map, onboarding, profiles, online, log);
        return new Fixture(growth, zone, online, log);
    }

    // Only the street COUNT is read back from a save; the legacy ids carry it
    // so the fixtures mirror a real driver file.
    private static string[]? LegacyStreets(int count)
    {
        if (count <= 1)
        {
            return null;
        }

        var ids = new string[count];
        ids[0] = OperatingZone.HomeStreetId;
        for (int i = 1; i < count; i++)
        {
            ids[i] = $"s{i}";
        }
        return ids;
    }

    // The placeholder map's numbers: the zone resumes at its base length and
    // one expansion adds the next street's own half-lap length.
    private static double StartKm => AddressBook.MapLengthKm(AddressMap.Default);
    private static double StreetKm => AddressBook.StreetLengthKm(
        AddressMap.Default, AddressMap.Default.Streets[1]);

    [Fact]
    public void Expand_OnTheCurve_RegistersTheNextStreetAndReportsTheGrowth()
    {
        // The first threshold is 2 settled jobs (Unity's curve); the home street
        // already counts, so the expanded zone answers 2 streets.
        var fixture = Build(onboardingDone: true, expansionJobs: 2);

        ZoneExpansion expansion = Assert.IsType<ZoneExpansion>(fixture.Growth.Expand());
        Assert.Equal("Street 2", expansion.StreetName);
        Assert.Equal(2, expansion.StreetCount);
        Assert.Equal(StartKm, expansion.PreviousKm, precision: 4);
        Assert.Equal(StartKm + StreetKm, expansion.NewKm, precision: 4);
        // The next threshold (3) re-draws on the first expansion only.
        Assert.Equal(3, expansion.JobsToNext);
        Assert.Equal(["home", "street-2"], fixture.Zone.StreetIds);
        Assert.Equal(1, fixture.Zone.ExpansionCount);
        Assert.Equal(0, fixture.Zone.CountedJobs);
        Assert.Equal(StartKm + StreetKm, fixture.Zone.Km, precision: 4);
        (double km, string name) = Assert.Single(fixture.Online.ZonePushes);
        Assert.Equal(fixture.Zone.Km, km, precision: 4);
        Assert.Equal(Driver, name);
        Assert.Contains(
            "[Zone] expanded: Street 2 joins (zone 2 streets",
            fixture.Log.Infos[^1]);
    }

    [Fact]
    public void Expand_AfterTheFirstExpansion_ClaimsTheNextMapStreet()
    {
        // One expansion already banked: the second threshold is 3 jobs, and the
        // street comes from the cached map order, so a relaunch with the two
        // legacy ids never re-registers a street.
        var fixture = Build(
            onboardingDone: true, expansionJobs: 2, expansions: 1, streets: 2,
            map: ThreeStreets);

        Assert.Null(fixture.Growth.Expand());
        Assert.Equal(["home", "s1"], fixture.Zone.StreetIds);
        Assert.Empty(fixture.Online.ZonePushes);

        fixture.Zone.RegisterJob();
        fixture.Growth.Expand();
        Assert.Equal(["home", "s1", "street-3"], fixture.Zone.StreetIds);
        Assert.Equal(2, fixture.Zone.ExpansionCount);
        Assert.Single(fixture.Online.ZonePushes);
    }

    [Fact]
    public void Expand_BeforeTheIntroduction_KeepsTheZone()
    {
        // Unity's firstSceneComplete gate: a fresh career stays on its home
        // street even when the counters already reached the curve.
        var fixture = Build(onboardingDone: false, expansionJobs: 2);

        Assert.Null(fixture.Growth.Expand());

        Assert.Equal(["home"], fixture.Zone.StreetIds);
        Assert.Equal(0, fixture.Zone.ExpansionCount);
        Assert.Equal(2, fixture.Zone.CountedJobs);
        Assert.Empty(fixture.Online.ZonePushes);
        Assert.Empty(fixture.Log.Infos);
    }

    [Fact]
    public void Expand_BelowTheCurve_KeepsTheZone()
    {
        // One settled job short of the first expansion: nothing moves and the
        // world board is not pushed.
        var fixture = Build(onboardingDone: true, expansionJobs: 1);

        Assert.Null(fixture.Growth.Expand());

        Assert.Equal(["home"], fixture.Zone.StreetIds);
        Assert.Equal(StartKm, fixture.Zone.Km, precision: 4);
        Assert.Empty(fixture.Online.ZonePushes);
        Assert.Empty(fixture.Log.Infos);
    }

    [Fact]
    public void Expand_AtTheEndOfTheStreetPool_ConsumesNothingAndLogsOnce()
    {
        // The street pool is fixed content: a two-street map with both streets
        // discovered is complete. The counter stays due, and later settles do
        // not repeat the completion log.
        var fixture = Build(onboardingDone: true, expansionJobs: 2, streets: 2);

        Assert.Null(fixture.Growth.Expand());
        Assert.Null(fixture.Growth.Expand());

        Assert.Equal(2, fixture.Zone.CountedJobs);
        Assert.Equal(0, fixture.Zone.ExpansionCount);
        Assert.Equal(2, fixture.Zone.StreetIds.Count);
        Assert.Empty(fixture.Online.ZonePushes);
        Assert.Contains("[Zone] street pool complete", Assert.Single(fixture.Log.Infos));
    }

    [Fact]
    public void Expand_WithoutAConfirmedProfile_PushesANamelessLength()
    {
        // Before the opening screen is answered the push stays nameless, the
        // same placeholder the XP and shift pushes use.
        var fixture = Build(onboardingDone: true, expansionJobs: 2, displayName: null);

        fixture.Growth.Expand();

        (double km, string name) = Assert.Single(fixture.Online.ZonePushes);
        Assert.Equal(fixture.Zone.Km, km, precision: 4);
        Assert.Equal(string.Empty, name);
    }
}
