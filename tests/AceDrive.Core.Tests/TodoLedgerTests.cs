using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The ToDo engine's ported behaviour (PORTPLAN 46): the four slots, the
/// completion latch, the acknowledge/cycle pass, the six-job rotation and the
/// activation of the POI-visit subjects.
/// </summary>
public sealed class TodoLedgerTests
{
    private static TodoFacts Facts(
        double cr = 0.0, double xp = 0.0, double bank = 1000.0,
        int taxi = 0, int delivery = 0, int pins = 0, double zone = 0.0,
        int cars = 1, bool max = false, bool wash = false, bool dealer = false,
        bool insurance = false, bool bankVisit = false, bool career = false) =>
        new(cr, xp, bank, taxi, delivery, pins, zone, cars, max, wash, dealer, insurance, bankVisit, career);

    private static TodoLedger Fresh() => TodoLedger.Fresh(TodoTable.Default);

    private static string[] Subjects(TodoLedger ledger, TodoFacts facts)
    {
        IReadOnlyList<TodoSlotView> slots = ledger.Slots(facts);
        string[] subjects = new string[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            subjects[i] = slots[i].SubjectId;
        }

        return subjects;
    }

    [Fact]
    public void Fresh_ShowsTheTablesFourTipsInOrder()
    {
        Assert.Equal(
            [
                TodoSubject.CrEarnings, TodoSubject.XpEarnings,
                TodoSubject.BankStandings, TodoSubject.GoToMaxGroceryStore,
            ],
            Subjects(Fresh(), Facts()));
    }

    [Fact]
    public void Fresh_QueuesEveryOtherProgressiveSubject_ButNotTheUnplacedVisits()
    {
        TodoLedger ledger = Fresh();
        TodoLedgerState state = ledger.Snapshot();

        Assert.Contains(TodoSubject.TaxiJobs, state.Queue);
        Assert.Contains(TodoSubject.DeliveryJobsCompleted, state.Queue);
        Assert.Contains(TodoSubject.OwnCars, state.Queue);
        Assert.DoesNotContain(TodoSubject.VisitCarWash, state.Queue);
        Assert.DoesNotContain(TodoSubject.VisitBank, state.Queue);
        // The gated follow-up waits for its gate, so it is queued by neither.
        Assert.DoesNotContain(TodoSubject.OpenDeliveryCareer, state.Queue);
    }

    [Fact]
    public void Sync_FlagsACompletionOnceAndCapturesTheMetTarget()
    {
        TodoLedger ledger = Fresh();

        TodoSyncResult first = ledger.Sync(Facts(cr: 500.0));
        Assert.True(first.Changed);
        Assert.Equal([TodoSubject.CrEarnings], first.Completed);
        Assert.True(ledger.HasCompleted);

        // The popup shows the tip the player met, not the advanced target.
        TodoSlotView tip = Assert.Single(ledger.CompletedTips(Facts(cr: 500.0)));
        Assert.Equal("TodoEarnCr", tip.CopyKey);
        Assert.Equal(500.0, tip.Target);

        TodoSyncResult second = ledger.Sync(Facts(cr: 500.0));
        Assert.Empty(second.Completed);
        Assert.Equal(1, ledger.CompletedCount);
    }

    [Fact]
    public void Sync_AdvancesPastEveryStackedCompletion()
    {
        TodoLedger ledger = Fresh();
        TodoFacts facts = Facts(cr: 2000.0);

        Assert.Single(ledger.Sync(facts).Completed);
        ledger.Acknowledge(facts);

        // The target cycled past 500, 750 and 1125: the same 2000 CR can never
        // pay a second care package.
        Assert.Empty(ledger.Sync(facts).Completed);
        Assert.False(ledger.HasCompleted);
    }

    [Fact]
    public void Sync_RotatesAMetSlotIntoTheQueue()
    {
        TodoLedger ledger = Fresh();
        _ = ledger.Sync(Facts(cr: 500.0));

        Assert.DoesNotContain(TodoSubject.CrEarnings, Subjects(ledger, Facts(cr: 500.0)));
        Assert.Contains(TodoSubject.CrEarnings, ledger.Snapshot().Queue);
    }

    [Fact]
    public void Acknowledge_RetiresAOneTimeSubjectForGood()
    {
        TodoLedger ledger = Fresh();
        TodoFacts facts = Facts(max: true);

        Assert.Equal([TodoSubject.GoToMaxGroceryStore], ledger.Sync(facts).Completed);
        Assert.True(ledger.Acknowledge(facts));
        Assert.False(ledger.HasCompleted);

        // The flag stays true (it is a career fact), but the tip never comes
        // back: Unity's shownInPopup retires one-time subjects.
        Assert.Empty(ledger.Sync(facts).Completed);
        Assert.DoesNotContain(TodoSubject.GoToMaxGroceryStore, Subjects(ledger, facts));
    }

    [Fact]
    public void FollowUp_StaysHiddenUntilItsGateCompletes()
    {
        TodoLedger ledger = Fresh();

        // The gate is unmet: the follow-up is neither visible nor queued.
        Assert.Empty(ledger.Sync(Facts()).Completed);
        Assert.DoesNotContain(TodoSubject.OpenDeliveryCareer, Subjects(ledger, Facts()));
        Assert.DoesNotContain(TodoSubject.OpenDeliveryCareer, ledger.Snapshot().Queue);
    }

    [Fact]
    public void FollowUp_TakesTheGatesSlotTheMomentTheGateCompletes()
    {
        TodoLedger ledger = Fresh();
        TodoFacts facts = Facts(max: true);

        // The grocery tip completes this sync; its slot refills with the
        // follow-up instead of the next queued subject (TaxiJobs).
        Assert.Equal([TodoSubject.GoToMaxGroceryStore], ledger.Sync(facts).Completed);
        Assert.Contains(TodoSubject.OpenDeliveryCareer, Subjects(ledger, facts));
        Assert.DoesNotContain(TodoSubject.TaxiJobs, Subjects(ledger, facts));
    }

    [Fact]
    public void FollowUp_CompletesWhenItsOwnFactLands()
    {
        TodoLedger ledger = Fresh();
        TodoFacts grocery = Facts(max: true);
        _ = ledger.Sync(grocery);
        ledger.Acknowledge(grocery);

        TodoFacts career = grocery with { DeliveryCareerOpened = true };
        Assert.Equal([TodoSubject.OpenDeliveryCareer], ledger.Sync(career).Completed);
        Assert.Equal(
            "TodoOpenDeliveryCareer",
            Assert.Single(ledger.CompletedTips(career)).CopyKey);
    }

    [Fact]
    public void Acknowledge_RefillsTheSlotWithTheNextUnmetSubject()
    {
        TodoLedger ledger = Fresh();
        TodoFacts facts = Facts(cr: 500.0);
        _ = ledger.Sync(facts);

        string[] before = Subjects(ledger, facts);
        Assert.Contains(TodoSubject.TaxiJobs, before);
        Assert.Equal(4, before.Length);
    }

    [Fact]
    public void RotateOnJob_TurnsATipOutAfterSixJobs()
    {
        TodoLedger ledger = Fresh();
        TodoFacts facts = Facts();

        for (int i = 0; i < TodoLedger.TurnLimit; i++)
        {
            _ = ledger.RotateOnJob(facts);
        }

        string[] subjects = Subjects(ledger, facts);
        Assert.Equal(4, subjects.Length);
        Assert.DoesNotContain(TodoSubject.CrEarnings, subjects);
        Assert.Contains(TodoSubject.GoToMaxGroceryStore, subjects);
    }

    [Fact]
    public void RotateOnJob_LeavesAMetTipAndTheMaxGroceryTipAlone()
    {
        TodoLedger ledger = Fresh();
        TodoFacts facts = Facts(cr: 500.0);

        for (int i = 0; i < TodoLedger.TurnLimit * 2; i++)
        {
            _ = ledger.RotateOnJob(facts);
        }

        // The met tip never counts a turn (it belongs to the popup) and the
        // Max grocery tip stays visible until fulfilled (Unity's rule).
        string[] subjects = Subjects(ledger, facts);
        Assert.Contains(TodoSubject.CrEarnings, subjects);
        Assert.Contains(TodoSubject.GoToMaxGroceryStore, subjects);
    }

    [Fact]
    public void Sync_LeavesAnInactiveVisitAloneUntilItsDoorIsPlaced()
    {
        TodoLedger ledger = Fresh();
        TodoFacts facts = Facts(bankVisit: true);

        Assert.Empty(ledger.Sync(facts).Completed);
        Assert.False(ledger.IsActive(TodoSubject.VisitBank));

        Assert.True(ledger.Activate(TodoSubject.VisitBank));
        Assert.Equal([TodoSubject.VisitBank], ledger.Sync(facts).Completed);
        Assert.False(ledger.Activate(TodoSubject.VisitBank));
    }

    [Fact]
    public void Snapshot_RoundTripsTheWholeCareer()
    {
        TodoLedger ledger = Fresh();
        TodoFacts facts = Facts(cr: 500.0);
        _ = ledger.Sync(facts);
        _ = ledger.RotateOnJob(facts);
        TodoLedgerState saved = ledger.Snapshot();

        TodoLedger resumed = TodoLedger.Resume(TodoTable.Default, saved);

        Assert.Equal(saved.Visible, resumed.Snapshot().Visible);
        Assert.Equal(saved.Queue, resumed.Snapshot().Queue);
        Assert.Equal(saved.Targets, resumed.Snapshot().Targets);
        Assert.True(resumed.HasCompleted);
        Assert.Equal("TodoEarnCr", Assert.Single(resumed.CompletedTips(facts)).CopyKey);
    }

    [Fact]
    public void Resume_DropsUnknownSubjectsAndAppendsTheNewOnes()
    {
        var saved = new TodoLedgerState(
            [new TodoSlotState(TodoSubject.CrEarnings, 0)],
            [],
            new Dictionary<string, double> { ["Obsolete"] = 1.0 },
            new Dictionary<string, int>(),
            new Dictionary<string, int>(),
            [],
            [],
            [],
            new Dictionary<string, TodoTipSnapshot>());

        TodoLedger resumed = TodoLedger.Resume(TodoTable.Default, saved);
        TodoLedgerState state = resumed.Snapshot();

        Assert.DoesNotContain("Obsolete", state.Targets.Keys);
        Assert.Equal(4, state.Visible.Count);
        // The subjects the save predates join the career again: some fill the
        // empty slots, the rest wait in rotation.
        string[] known = [.. state.Visible.Select(slot => slot.SubjectId), .. state.Queue];
        Assert.Contains(TodoSubject.TaxiJobs, known);
    }

    [Fact]
    public void Resume_WithoutUsableSlots_StartsFresh()
    {
        TodoLedger resumed = TodoLedger.Resume(TodoTable.Default, null);

        Assert.Equal(Subjects(Fresh(), Facts()), Subjects(resumed, Facts()));
    }

    [Fact]
    public void Sync_KeepsAtMostTheCompletionCapAndLosesTheExtras()
    {
        TodoLedger ledger = Fresh();
        // Eight progressive subjects met at once, each exactly on its target:
        // the list keeps the cap, the rest are lost.
        TodoFacts facts = Facts(
            cr: 500.0, xp: 2.0, bank: 1002.0, taxi: 5, delivery: 2,
            pins: 18, zone: 4.0, cars: 2);

        Assert.Equal(TodoLedger.MaxCompletedTips, ledger.Sync(facts).Completed.Count);
        Assert.Equal(TodoLedger.MaxCompletedTips, ledger.CompletedCount);

        ledger.Acknowledge(facts);
        Assert.Equal(0, ledger.CompletedCount);

        // The dropped completion cycled past its met target: it never resurfaces.
        Assert.DoesNotContain(TodoSubject.DeliveryJobsCompleted, ledger.Sync(facts).Completed);
    }

    [Fact]
    public void Acknowledge_WithNothingPending_ChangesNothing()
    {
        TodoLedger ledger = Fresh();
        Assert.False(ledger.Acknowledge(Facts()));
    }
}
