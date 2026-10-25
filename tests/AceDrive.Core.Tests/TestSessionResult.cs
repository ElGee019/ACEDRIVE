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
        OnboardingFlow story, TodoBoard todos)
    {
        Vm = vm;
        Clock = clock;
        Tel = tel;
        Store = store;
        Story = story;
        Todos = todos;
    }

    public MainViewModel Vm { get; }

    public FakeClock Clock { get; }

    public FakeTelemetry Tel { get; }

    public FakeStore Store { get; }

    /// <summary>The introduction walk the root hands the coordinator.</summary>
    public OnboardingFlow Story { get; }

    /// <summary>The ToDo list the jobs page reads (PORTPLAN 46).</summary>
    public TodoBoard Todos { get; }

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
