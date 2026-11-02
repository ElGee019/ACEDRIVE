// "Reach {target} destinations without using GPS." (PORTPLAN item 46, Unity
// SetDestinationsManuallyEvaluator): the target steps 18 at a time.
//
// WHY it counts settled jobs, not Unity's manual pins: the overlay has no GPS
// yet, so every drive is a drive without GPS and every settled job is one
// destination reached. Unity's pin count measured the drive set on a map, an
// action this port has no equivalent for until navigation lands; revisit then.
namespace AceDrive.Core;

public sealed class SetDestinationsRequirement : ITodoRequirement
{
    private const double Step = 18.0;

    public string Subject => TodoSubject.SetDestinationsManually;
    public bool IsOneTime => false;
    public double InitialTarget => Step;
    public string TipKey => "TodoSetDestinations";
    public string? PoiType => null;
    public string? FallbackKey => null;

    public double Current(TodoFacts facts) => facts.DestinationsReached;

    public bool IsMet(TodoFacts facts, double target) =>
        facts.DestinationsReached >= target;

    // Unity: 18 + 18 * subjectProgress.
    public TodoCycle Advance(TodoCycle cycle) =>
        cycle with { Target = Step + Step * cycle.Progress };
}
