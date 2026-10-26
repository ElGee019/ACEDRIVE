using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Cento Miglia page runs headless: the header copy, the tier gates,
/// the picked tier's detail panel, the ENTER flow's registration number and
/// build countdown, and the voice actions all resolve from the tier table and
/// resx (Unity CentoMigliaScreen's CentoA/CentoB panels and its ENTER
/// coroutine).</summary>
public sealed class CentoMigliaScreenTests
{
    private static readonly CentoTier[] Fixture =
    [
        new("gentlemen_drivers", "Gentlemen Drivers Club", "Free for all.", 1),
        new("era_event", "{0}s Era Only", "Exclusive event.", 2),
        new("event3", "Convertibles Club", "Description for Event 3", 4),
    ];

    private sealed record Harness(
        CentoMigliaScreenViewModel Page, FakeClock Clock,
        List<CentoMigliaEvent> Built, List<int> Exits);

    private static CentoMigliaFacts Facts(
        double reputation = 1.5, bool streets = true,
        int cars = 2, int carClass = 16, int week = 3, int year = 1924,
        double routeKm = 0.9645) =>
        new(week, year, reputation, streets, cars, carClass,
            new CentoRoutePlan(
                new AddressLocation("Street 1", 1, HouseSide.Right),
                new AddressLocation("Street 2", 1, HouseSide.Right),
                routeKm));

    private static Harness Create(
        CentoMigliaFacts? facts = null, IReadOnlyList<CentoTier>? tiers = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var built = new List<CentoMigliaEvent>();
        var exits = new List<int>();
        var page = new CentoMigliaScreenViewModel(
            tiers ?? Fixture,
            facts ?? Facts(),
            new RelayCommand(() => exits.Add(exits.Count)),
            clock,
            new Random(0),
            built.Add);
        return new Harness(page, clock, built, exits);
    }

    // The event is entered exactly the way the player does: pick the row, then
    // ENTER on the detail panel.
    private static Harness Entered(
        CentoMigliaFacts? facts = null, IReadOnlyList<CentoTier>? tiers = null,
        int tier = 0)
    {
        Harness h = Create(facts, tiers);
        h.Page.TierCommand.Execute(h.Page.Tiers[tier]);
        h.Page.EnterCommand.Execute(null);
        return h;
    }

    [Fact]
    public void Header_PrintsTheWeekAndTheFlooredReputation()
    {
        Harness h = Create(Facts(reputation: 0.0, week: 3));
        Assert.Equal("Week 3", h.Page.WeekLine);
        // A fresh driver's raw 0 prints as Unity's 1.00000 floor.
        Assert.Equal("YOUR CLUB REPUTATION: 1.00000", h.Page.ReputationLine);
    }

    [Fact]
    public void Tiers_PrintTheLevelEachClubAsksFor()
    {
        Harness h = Create(Facts(reputation: 1.5));
        Assert.Equal(3, h.Page.Tiers.Count);
        Assert.Equal("Gentlemen Drivers Club", h.Page.Tiers[0].Label);
        Assert.Equal("Requires Reputation Level 1", h.Page.Tiers[0].Requirement);
        // The era tier's decade comes from the game year, not the data file.
        Assert.Equal("1920s Era Only", h.Page.Tiers[1].Label);
        Assert.Equal("Requires Reputation Level 2", h.Page.Tiers[1].Requirement);
        Assert.Equal("Requires Reputation Level 4", h.Page.Tiers[2].Requirement);
    }

    [Fact]
    public void Tiers_GateOnTheReputationsAndTheStreets()
    {
        Harness h = Create(Facts(reputation: 1.5));
        Assert.True(h.Page.Tiers[0].Eligible);
        Assert.False(h.Page.Tiers[1].Eligible);
        Assert.False(h.Page.Tiers[2].Eligible);

        Harness reached = Create(Facts(reputation: 4.0));
        Assert.True(reached.Page.Tiers[1].Eligible);
        Assert.True(reached.Page.Tiers[2].Eligible);

        // No discovered street closes even the free club (Unity's gate).
        Harness lost = Create(Facts(reputation: 4.0, streets: false));
        Assert.All(lost.Page.Tiers, tier => Assert.False(tier.Eligible));
    }

    [Fact]
    public void Page_OpensOnTheTierList()
    {
        Harness h = Create();
        Assert.True(h.Page.IsTierList);
        Assert.False(h.Page.IsDetail);
        Assert.False(h.Page.SetupVisible);
        Assert.Equal(string.Empty, h.Page.EligibilityText);
    }

    [Fact]
    public void LockedTier_NeverOpensItsDetail()
    {
        Harness h = Create(Facts(reputation: 1.5));
        h.Page.TierCommand.Execute(h.Page.Tiers[1]);
        Assert.True(h.Page.IsTierList);
    }

    [Fact]
    public void SelectingATier_OpensItsDescriptionAndEligibility()
    {
        Harness h = Create(Facts(reputation: 2.0, cars: 2, carClass: 16));
        h.Page.TierCommand.Execute(h.Page.Tiers[1]);

        Assert.True(h.Page.IsDetail);
        Assert.Equal("1920s Era Only", h.Page.DetailLabel);
        Assert.Equal("Exclusive event.", h.Page.DetailDescription);
        Assert.Equal(
            "Club Reputation: 2 / 2\nYou own 2 cars\nThe car class of your current car: 16",
            h.Page.EligibilityText);
    }

    [Fact]
    public void SelectingATier_EnablesTheEnterButton()
    {
        // ENTER's IsEnabled binds to CanEnter, so the page has to announce the
        // flag when the detail opens: a silent flip leaves WPF's button
        // disabled and swallows every click.
        Harness h = Create();
        var raised = new List<string?>();
        h.Page.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        h.Page.TierCommand.Execute(h.Page.Tiers[0]);

        Assert.True(h.Page.CanEnter);
        Assert.Contains(nameof(CentoMigliaScreenViewModel.CanEnter), raised);
    }

    [Fact]
    public void FreeTier_PrintsFreeEntryAndTheSingularCarCount()
    {
        Harness h = Create(Facts(reputation: 0.0, cars: 1, carClass: 0));
        h.Page.TierCommand.Execute(h.Page.Tiers[0]);
        Assert.Equal(
            "Club Reputation: Free entry\nYou own 1 car\nThe car class of your current car: 0",
            h.Page.EligibilityText);
    }

    [Fact]
    public void Back_LeavesTheDetailThenThePage()
    {
        Harness h = Create();
        h.Page.TierCommand.Execute(h.Page.Tiers[0]);
        Assert.True(h.Page.IsDetail);

        h.Page.BackCommand.Execute(null);
        Assert.True(h.Page.IsTierList);
        Assert.Empty(h.Exits);

        h.Page.BackCommand.Execute(null);
        Assert.Single(h.Exits);
    }

    [Fact]
    public void Enter_StartsTheBuildWithARegistrationNumberAndTheCountdown()
    {
        Harness h = Entered();

        Assert.True(h.Page.IsBuilding);
        Assert.False(h.Page.IsDetail);
        Assert.True(h.Page.SetupVisible);
        Assert.Equal("BUILDING EVENT", h.Page.SetupTitle);
        Assert.Equal("Gentlemen Drivers Club", h.Page.DetailLabel);
        // The first club issues one digit.
        Assert.Matches("^Registration #[0-9]$", h.Page.RegistrationLine);
        Assert.Equal("Starting in 4s", h.Page.TimerLine);
        Assert.Equal("SETTING UP…", h.Page.EnterLabel);
        Assert.True(h.Page.ShowEnter);
        Assert.False(h.Page.CanEnter);
        Assert.False(h.Page.ShowBack);
        Assert.Empty(h.Built);
    }

    [Fact]
    public void Build_CountsDownOnWholeSeconds()
    {
        Harness h = Entered();

        h.Clock.Advance(TimeSpan.FromSeconds(0.1));
        h.Page.Tick(h.Clock.UtcNow);
        Assert.Equal("Starting in 4s", h.Page.TimerLine);

        h.Clock.Advance(TimeSpan.FromSeconds(0.9));
        h.Page.Tick(h.Clock.UtcNow);
        Assert.Equal("Starting in 3s", h.Page.TimerLine);

        h.Clock.Advance(TimeSpan.FromSeconds(3));
        h.Page.Tick(h.Clock.UtcNow);
        Assert.Equal("EVENT READY", h.Page.SetupTitle);
        Assert.True(h.Page.IsBuilt);
        Assert.True(h.Page.ShowBack);
        Assert.False(h.Page.ShowEnter);
    }

    [Fact]
    public void Build_ShowsTheLongestDistanceTheZoneAllows()
    {
        // The route is the furthest two doors the zone holds, so a grown zone is
        // a longer event (Unity ranks its discovered street pairs, furthest
        // first). The page reports the planned route's own length.
        Harness h = Entered(Facts(routeKm: 2.8935));
        h.Clock.Advance(TimeSpan.FromSeconds(4));
        h.Page.Tick(h.Clock.UtcNow);

        Assert.Equal("Longest distance: 2.9 km", h.Page.DistanceLine);
        CentoMigliaEvent built = Assert.Single(h.Built);
        Assert.Equal(1, built.TierNumber);
        Assert.Equal("Gentlemen Drivers Club", built.TierLabel);
        Assert.Equal(2.8935, built.Route.DistanceKm, precision: 6);
        Assert.Equal(new AddressLocation("Street 1", 1, HouseSide.Right), built.Route.Start);
        Assert.Equal(new AddressLocation("Street 2", 1, HouseSide.Right), built.Route.Finish);
        Assert.Matches("^[0-9]$", built.Registration.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Build_IssuesThePickedTiersRegistrationLength()
    {
        // Tier 3 (the third club) issues two digits, like Unity's index table.
        Harness h = Entered(Facts(reputation: 4.0), tier: 2);
        Assert.Matches("^Registration #[1-9][0-9]$", h.Page.RegistrationLine);
    }

    [Fact]
    public void Build_BlocksEveryWayOutUntilTheEventIsReady()
    {
        Harness h = Entered();

        h.Page.BackCommand.Execute(null);
        Assert.True(h.Page.IsBuilding);
        Assert.Empty(h.Exits);
        // Nothing answers by voice either: Unity disables both buttons.
        Assert.Empty(h.Page.VoiceActions);

        h.Clock.Advance(TimeSpan.FromSeconds(4));
        h.Page.Tick(h.Clock.UtcNow);
        Assert.Contains("BACK", h.Page.VoiceActions.Keys);

        h.Page.BackCommand.Execute(null);
        Assert.Single(h.Exits);
    }

    [Fact]
    public void Build_ReportsTheEventExactlyOnce()
    {
        Harness h = Entered();
        h.Clock.Advance(TimeSpan.FromSeconds(4));
        h.Page.Tick(h.Clock.UtcNow);
        Assert.Single(h.Built);

        h.Clock.Advance(TimeSpan.FromSeconds(30));
        h.Page.Tick(h.Clock.UtcNow);
        Assert.Single(h.Built);
    }

    [Fact]
    public void Voice_ListAnswersToUnlockedClubsAndBack()
    {
        Harness h = Create(Facts(reputation: 1.5));
        Assert.True(h.Page.VoiceActions.ContainsKey("Gentlemen Drivers Club"));
        // A locked club has no button to speak to, so it has no keyword either.
        Assert.DoesNotContain("1920s Era Only", h.Page.VoiceActions.Keys);
        Assert.True(h.Page.VoiceActions.ContainsKey("BACK"));

        h.Page.VoiceActions["Gentlemen Drivers Club"].Invoke();
        Assert.True(h.Page.IsDetail);
        Assert.True(h.Page.VoiceActions.ContainsKey("ENTER EVENT"));
        Assert.DoesNotContain("Convertibles Club", h.Page.VoiceActions.Keys);

        h.Page.VoiceActions["BACK"].Invoke();
        Assert.True(h.Page.IsTierList);

        Harness leaving = Create();
        leaving.Page.VoiceActions["BACK"].Invoke();
        Assert.Single(leaving.Exits);
    }

    [Fact]
    public void MissingTierTable_LeavesAHeaderAndTheWayOut()
    {
        Harness h = Create(tiers: []);
        Assert.Empty(h.Page.Tiers);
        Assert.True(h.Page.VoiceActions.ContainsKey("BACK"));
    }
}
