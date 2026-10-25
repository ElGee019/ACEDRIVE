using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Cento Miglia page runs headless: the header copy, the tier gates,
/// the picked tier's detail panel and its voice actions all resolve from the
/// tier table and resx (Unity CentoMigliaScreen's CentoA/CentoB panels).</summary>
public sealed class CentoMigliaScreenTests
{
    private static readonly CentoTier[] Fixture =
    [
        new("gentlemen_drivers", "Gentlemen Drivers Club", "Free for all.", 1),
        new("era_event", "{0}s Era Only", "Exclusive event.", 2),
        new("event3", "Convertibles Club", "Description for Event 3", 4),
    ];

    private static CentoMigliaFacts Facts(
        double reputation = 1.5, bool streets = true,
        int cars = 2, int carClass = 16, int week = 3, int year = 1924) =>
        new(week, year, reputation, streets, cars, carClass);

    private static CentoMigliaScreenViewModel Create(
        CentoMigliaFacts? facts = null, Action? close = null,
        IReadOnlyList<CentoTier>? tiers = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        return new CentoMigliaScreenViewModel(
            tiers ?? Fixture,
            facts ?? Facts(),
            new RelayCommand(() => close?.Invoke()));
    }

    [Fact]
    public void Header_PrintsWeekYearAndTheFlooredReputation()
    {
        var vm = Create(Facts(reputation: 0.0, week: 3));
        Assert.Equal("Week 3, 1924", vm.WeekLine);
        // A fresh driver's raw 0 prints as Unity's 1.00000 floor.
        Assert.Equal("YOUR CLUB REPUTATION: 1.00000", vm.ReputationLine);
    }

    [Fact]
    public void Tiers_PrintTheLevelEachClubAsksFor()
    {
        var vm = Create(Facts(reputation: 1.5));
        Assert.Equal(3, vm.Tiers.Count);
        Assert.Equal("Gentlemen Drivers Club", vm.Tiers[0].Label);
        Assert.Equal("Requires Reputation Level 1", vm.Tiers[0].Requirement);
        // The era tier's decade comes from the game year, not the data file.
        Assert.Equal("1920s Era Only", vm.Tiers[1].Label);
        Assert.Equal("Requires Reputation Level 2", vm.Tiers[1].Requirement);
        Assert.Equal("Requires Reputation Level 4", vm.Tiers[2].Requirement);
    }

    [Fact]
    public void Tiers_GateOnTheReputationsAndTheStreets()
    {
        var vm = Create(Facts(reputation: 1.5));
        Assert.True(vm.Tiers[0].Eligible);
        Assert.False(vm.Tiers[1].Eligible);
        Assert.False(vm.Tiers[2].Eligible);

        var reached = Create(Facts(reputation: 4.0));
        Assert.True(reached.Tiers[1].Eligible);
        Assert.True(reached.Tiers[2].Eligible);

        // No discovered street closes even the free club (Unity's gate).
        var lost = Create(Facts(reputation: 4.0, streets: false));
        Assert.All(lost.Tiers, tier => Assert.False(tier.Eligible));
    }

    [Fact]
    public void Page_OpensOnTheTierList()
    {
        var vm = Create();
        Assert.True(vm.IsTierList);
        Assert.False(vm.IsDetail);
        Assert.Equal(string.Empty, vm.EligibilityText);
    }

    [Fact]
    public void LockedTier_NeverOpensItsDetail()
    {
        var vm = Create(Facts(reputation: 1.5));
        vm.TierCommand.Execute(vm.Tiers[1]);
        Assert.True(vm.IsTierList);
    }

    [Fact]
    public void SelectingATier_OpensItsDescriptionAndEligibility()
    {
        var vm = Create(Facts(reputation: 2.0, cars: 2, carClass: 16));
        vm.TierCommand.Execute(vm.Tiers[1]);

        Assert.True(vm.IsDetail);
        Assert.Equal("1920s Era Only", vm.DetailLabel);
        Assert.Equal("Exclusive event.", vm.DetailDescription);
        Assert.Equal(
            "Club Reputation: 2 / 2\nYou own 2 cars\nThe car class of your current car: 16",
            vm.EligibilityText);
    }

    [Fact]
    public void FreeTier_PrintsFreeEntryAndTheSingularCarCount()
    {
        var vm = Create(Facts(reputation: 0.0, cars: 1, carClass: 0));
        vm.TierCommand.Execute(vm.Tiers[0]);
        Assert.Equal(
            "Club Reputation: Free entry\nYou own 1 car\nThe car class of your current car: 0",
            vm.EligibilityText);
    }

    [Fact]
    public void Back_LeavesTheDetailThenThePage()
    {
        int closed = 0;
        var vm = Create(close: () => closed++);
        vm.TierCommand.Execute(vm.Tiers[0]);
        Assert.True(vm.IsDetail);

        vm.BackCommand.Execute(null);
        Assert.True(vm.IsTierList);
        Assert.Equal(0, closed);

        vm.BackCommand.Execute(null);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Enter_ClosesThePage()
    {
        int closed = 0;
        var vm = Create(close: () => closed++);
        vm.TierCommand.Execute(vm.Tiers[0]);
        vm.EnterCommand.Execute(null);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Voice_ListAnswersToUnlockedClubsAndBack()
    {
        var vm = Create(Facts(reputation: 1.5));
        Assert.True(vm.VoiceActions.ContainsKey("Gentlemen Drivers Club"));
        // A locked club has no button to speak to, so it has no keyword either.
        Assert.DoesNotContain("1920s Era Only", vm.VoiceActions.Keys);
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));

        vm.VoiceActions["Gentlemen Drivers Club"].Invoke();
        Assert.True(vm.IsDetail);
        Assert.True(vm.VoiceActions.ContainsKey("ENTER EVENT"));
        Assert.DoesNotContain("Convertibles Club", vm.VoiceActions.Keys);

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.IsTierList);

        int closed = 0;
        var leaving = Create(close: () => closed++);
        leaving.VoiceActions["BACK"].Invoke();
        Assert.Equal(1, closed);
    }

    [Fact]
    public void MissingTierTable_LeavesAHeaderAndTheWayOut()
    {
        var vm = Create(tiers: []);
        Assert.Empty(vm.Tiers);
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));
    }
}
