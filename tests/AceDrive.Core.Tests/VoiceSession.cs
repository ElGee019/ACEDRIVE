using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The voice suite's session: the shared graph plus the two fakes the suite
/// builds itself (its mic and its car watcher), so a test can drive the walk
/// through <see cref="Story"/> and still speak into its own listener.
///
/// WHY the deconstruction: the tests that only speak read exactly like they did
/// when the builder returned a tuple; only the ones that walk the introduction
/// take the session itself.
/// </summary>
internal sealed class VoiceSession
{
    public VoiceSession(
        TestSessionResult session, FakeClock clock, FakeVoiceListener voice,
        FakeCarBrandSource car)
    {
        Session = session;
        Clock = clock;
        Voice = voice;
        Car = car;
    }

    public TestSessionResult Session { get; }

    public FakeClock Clock { get; }

    public FakeVoiceListener Voice { get; }

    public FakeCarBrandSource Car { get; }

    public MainViewModel Vm => Session.Vm;

    /// <summary>The introduction walk the root hands the coordinator.</summary>
    public OnboardingFlow Story => Session.Story;

    public void Deconstruct(
        out MainViewModel vm, out FakeClock clock, out FakeVoiceListener voice,
        out FakeCarBrandSource car)
    {
        vm = Vm;
        clock = Clock;
        voice = Voice;
        car = Car;
    }
}
