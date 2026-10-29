using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// One built session: the coordinator the tests drive, the fakes they assert
/// on, and the story flow that owns the introduction.
///
/// WHY the deconstruction: most suites only need the VM and read exactly like
/// they did when the builder returned a tuple, so a slice that changes one
/// collaborator does not fan out into every test. A suite that drives the walk
/// takes <see cref="Story"/> directly — the coordinator no longer forwards the
/// story's verbs or flags, so the tests reach the collaborator production
/// reaches.
/// </summary>
internal sealed class TestSessionResult
{
    public TestSessionResult(
        MainViewModel vm, FakeClock clock, FakeTelemetry tel, FakeStore store,
        OnboardingFlow story, TodoBoard todos, ValuationFlow valuation,
        ValuationLedger ledger, IValuationBoard board,
        WhereIsWillyFlow willy, WhereIsWillyLedger willyLedger,
        RoadTestFlow roadTest)
    {
        Vm = vm;
        Clock = clock;
        Tel = tel;
        Store = store;
        Story = story;
        Todos = todos;
        Valuation = valuation;
        Ledger = ledger;
        Board = board;
        Willy = willy;
        WillyLedger = willyLedger;
        RoadTest = roadTest;
    }

    public MainViewModel Vm { get; }

    public FakeClock Clock { get; }

    public FakeTelemetry Tel { get; }

    public FakeStore Store { get; }

    /// <summary>The introduction walk the root hands the coordinator.</summary>
    public OnboardingFlow Story { get; }

    /// <summary>The ToDo list the jobs page reads (PORTPLAN 46).</summary>
    public TodoBoard Todos { get; }

    /// <summary>The weekly market valuation's flow (PORTPLAN 49).</summary>
    public ValuationFlow Valuation { get; }

    /// <summary>The valuation's persisted half the assertions read.</summary>
    public ValuationLedger Ledger { get; }

    /// <summary>The list the flow reads and pushes to.</summary>
    public IValuationBoard Board { get; }

    /// <summary>The weekly missing-person run's flow (ToWhereIsWilly.md).</summary>
    public WhereIsWillyFlow Willy { get; }

    /// <summary>The missing-person run's persisted completion stamp.</summary>
    public WhereIsWillyLedger WillyLedger { get; }

    /// <summary>The road test's flow (PORTPLAN 49).</summary>
    public RoadTestFlow RoadTest { get; }
    public void Deconstruct(
        out MainViewModel vm, out FakeClock clock, out FakeTelemetry tel,
        out FakeStore store)
    {
        vm = Vm;
        clock = Clock;
        tel = Tel;
        store = Store;
    }
}
