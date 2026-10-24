using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The operating-zone growth on its own (PORTPLAN 58): the introduction gate,
/// the threshold curve and the world push the settle hook triggers. The
/// coordinator is not involved, so each test sees only this collaborator's
/// decisions: which gate held, which street joined and what was pushed.
/// </summary>
public sealed class ZoneGrowthTests
{
    private const string Driver = "ElGee";

    private sealed record Fixture(
        ZoneGrowth Growth, OperatingZone Zone, FakePlayerTelemetry Online, TestLog Log);

    public ZoneGrowthTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static Fixture Build(
        bool onboardingDone, int expansionJobs = 0, int expansions = 0,
        string? displayName = Driver)
    {
        AddressMap map = AddressMap.Default;
        var zone = new OperatingZone(
            new DriverState(0.0, 0.0, ExpansionJobs: expansionJobs, ExpansionCount: expansions),
            seed: 0, AddressBook.MapLengthKm(map));
        var onboarding = new FakeOnboardingStore();
        onboarding.Save(OnboardingState.Fresh with { BirthdayDone = onboardingDone });
        IProfileStore profiles = displayName is null
            ? new FakeProfileStore()
            : new FakeProfileStore(
                new PlayerProfile(displayName, "Germany", "Brands", "street-1", 1));
        var online = new FakePlayerTelemetry();
        var log = new TestLog();
        var growth = new ZoneGrowth(zone, map, onboarding, profiles, online, log);
        return new Fixture(growth, zone, online, log);
    }

    // The placeholder map's numbers: the zone resumes at its length and one
    // expansion adds the mean street, exactly like App.OnStartup seeds it.
    private static double StartKm => AddressBook.MapLengthKm(AddressMap.Default);
    private static double StreetKm => AddressBook.AverageStreetLengthKm(AddressMap.Default);

    [Fact]
    public void Expand_OnTheCurve_RegistersTheNextStreetAndPushesTheLength()
    {
        // The first threshold is 2 settled jobs (Unity's curve); the home street
        // already counts, so the expanded zone answers 2 streets.
        var fixture = Build(onboardingDone: true, expansionJobs: 2);

        fixture.Growth.Expand();

        Assert.Equal(["home", "s1"], fixture.Zone.StreetIds);
        Assert.Equal(1, fixture.Zone.ExpansionCount);
        Assert.Equal(0, fixture.Zone.CountedJobs);
        Assert.Equal(StartKm + StreetKm, fixture.Zone.Km, precision: 4);
        (double km, string name) = Assert.Single(fixture.Online.ZonePushes);
        Assert.Equal(fixture.Zone.Km, km, precision: 4);
        Assert.Equal(Driver, name);
        Assert.Contains("[Zone] expanded: s1 joins (zone 2 streets", fixture.Log.Infos[^1]);
    }

    [Fact]
    public void Expand_AfterTheFirstExpansion_ClaimsTheNextInterimStreet()
    {
        // One expansion already banked: the second threshold is 3 jobs, and the
        // interim id follows the count so a relaunch never re-registers "s1".
        var fixture = Build(onboardingDone: true, expansionJobs: 2, expansions: 1);

        fixture.Growth.Expand();
        Assert.Equal(["home"], fixture.Zone.StreetIds);
        Assert.Empty(fixture.Online.ZonePushes);

        fixture.Zone.RegisterJob();
        fixture.Growth.Expand();
        Assert.Equal(["home", "s2"], fixture.Zone.StreetIds);
        Assert.Equal(2, fixture.Zone.ExpansionCount);
        Assert.Single(fixture.Online.ZonePushes);
    }

    [Fact]
    public void Expand_BeforeTheIntroduction_KeepsTheZone()
    {
        // Unity's firstSceneComplete gate: a fresh career stays on its home
        // street even when the counters already reached the curve.
        var fixture = Build(onboardingDone: false, expansionJobs: 2);

        fixture.Growth.Expand();

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

        fixture.Growth.Expand();

        Assert.Equal(["home"], fixture.Zone.StreetIds);
        Assert.Equal(StartKm, fixture.Zone.Km, precision: 4);
        Assert.Empty(fixture.Online.ZonePushes);
        Assert.Empty(fixture.Log.Infos);
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
