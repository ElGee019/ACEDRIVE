using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The community centre's hiring rules (PORTPLAN 54, Unity
/// CommunityCentre.RefreshHireRoster/OnHireClicked): a fresh five per display
/// week, a refill inside the same week, never offering the hired, and every
/// change banked through the store.
/// </summary>
public sealed class EmployeeRosterTests
{
    private static readonly string[] Town =
    [
        "Anna Berg", "Bram de Wit", "Carla Rossi", "Daan Vos", "Elsa Meyer", "Finn Marsh",
    ];

    private static readonly string[] NewTown =
    [
        "Gina Holt", "Hugo Smit", "Ines Vidal", "Jo Kappel", "Kees Aalders",
    ];

    private static readonly string[] OneEmployee = ["Anna Berg"];

    private static EmployeeRoster Create(FakeEmployeeStore store, int seed = 7) =>
        new(store, new Random(seed));

    [Fact]
    public void FirstVisit_RollsFiveDistinctCandidatesAndBanksTheWeek()
    {
        var store = new FakeEmployeeStore();
        var roster = Create(store);

        roster.RefreshCandidates(Town, week: 1);

        Assert.Equal(EmployeeRoster.CandidateCount, roster.Candidates.Count);
        Assert.Equal(5, roster.Candidates.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(roster.Candidates, name => Assert.Contains(name, Town));
        Assert.Equal(1, roster.RosterWeek);
        Assert.Single(store.Saves);
    }

    [Fact]
    public void NewWeek_RollsAFreshRosterFromTheNewTown()
    {
        var store = new FakeEmployeeStore();
        var roster = Create(store);
        roster.RefreshCandidates(Town, week: 1);

        roster.RefreshCandidates(NewTown, week: 2);

        Assert.Equal(2, roster.RosterWeek);
        Assert.All(roster.Candidates, name => Assert.DoesNotContain(name, Town));
        Assert.All(roster.Candidates, name => Assert.Contains(name, NewTown));
    }

    [Fact]
    public void SameWeek_KeepsTheRosterAndTopsItBackUpAfterAHire()
    {
        var store = new FakeEmployeeStore();
        var roster = Create(store);
        roster.RefreshCandidates(Town, week: 1);
        string hired = roster.Candidates[0];
        Assert.True(roster.Hire(hired));

        roster.RefreshCandidates(Town, week: 1);

        Assert.Equal(EmployeeRoster.CandidateCount, roster.Candidates.Count);
        Assert.DoesNotContain(hired, roster.Candidates);
        Assert.True(roster.IsEmployed(hired));
        // One candidate was hired, so the one resident left off the first draw
        // is what the refill offers.
        Assert.All(roster.Candidates, name => Assert.Contains(name, Town));
    }

    [Fact]
    public void Hire_MovesTheCandidateToTheCrewAndSavesImmediately()
    {
        var store = new FakeEmployeeStore();
        var roster = Create(store);
        roster.RefreshCandidates(Town, week: 1);
        int saves = store.Saves.Count;
        string hired = roster.Candidates[0];

        Assert.True(roster.Hire(hired));

        Assert.True(roster.IsEmployed(hired));
        Assert.DoesNotContain(hired, roster.Candidates);
        Assert.Equal(saves + 1, store.Saves.Count);
        Assert.Contains(hired, store.State.Employees!);
        Assert.DoesNotContain(hired, store.State.Candidates!);
    }

    [Fact]
    public void Hire_RefusesABlankUnknownOrAlreadyHiredName()
    {
        var store = new FakeEmployeeStore();
        var roster = Create(store);
        roster.RefreshCandidates(Town, week: 1);

        Assert.False(roster.Hire(""));
        Assert.False(roster.Hire("Nobody At All"));
        Assert.True(roster.Hire(roster.Candidates[0]));
        Assert.False(roster.Hire(roster.Employees[0]));
        Assert.Single(roster.Employees);
    }

    [Fact]
    public void Refresh_NeverOffersSomebodyAlreadyOnTheCrew()
    {
        var store = new FakeEmployeeStore();
        var roster = Create(store);
        roster.RefreshCandidates(Town, week: 1);
        string hired = roster.Candidates[0];
        Assert.True(roster.Hire(hired));

        // The next week re-rolls from the same town, which still holds the
        // resident: the crew wins and the name stays off the roster.
        roster.RefreshCandidates(Town, week: 2);

        Assert.DoesNotContain(hired, roster.Candidates);
    }

    [Fact]
    public void Refresh_SameWeek_DropsACandidateWhoLeftTheTown()
    {
        var store = new FakeEmployeeStore();
        var roster = Create(store);
        roster.RefreshCandidates(Town, week: 1);
        string gone = roster.Candidates[0];

        roster.RefreshCandidates(
            Town.Where(name => name != gone).ToArray(), week: 1);

        Assert.DoesNotContain(gone, roster.Candidates);
        Assert.Equal(EmployeeRoster.CandidateCount, roster.Candidates.Count);
    }

    [Fact]
    public void Load_RestoresTheCrewAndDropsACandidateAlreadyHired()
    {
        var store = new FakeEmployeeStore
        {
            State = new EmployeeState(
                Employees: ["Anna Berg"],
                Candidates: ["Anna Berg", "Bram de Wit", "Carla Rossi"],
                RosterWeek: 4),
        };

        var roster = Create(store);

        Assert.Equal(OneEmployee, roster.Employees);
        Assert.Contains("Bram de Wit", roster.Candidates);
        Assert.DoesNotContain("Anna Berg", roster.Candidates);
        Assert.Equal(4, roster.RosterWeek);
    }

    [Fact]
    public void EmptyTown_LeavesNobodyToOffer()
    {
        var store = new FakeEmployeeStore();
        var roster = Create(store);

        roster.RefreshCandidates([], week: 1);

        Assert.Empty(roster.Candidates);
        Assert.Empty(roster.Employees);
    }
}
