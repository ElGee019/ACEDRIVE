// "Set {target} destinations manually." (PORTPLAN item 46, Unity
// SetDestinationsManuallyEvaluator): the address page's pins count, and the
// target steps 18 at a time.
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

    public double Current(TodoFacts facts) => facts.ManuallySetDestinations;

    public bool IsMet(TodoFacts facts, double target) =>
        facts.ManuallySetDestinations >= target;

    // Unity: 18 + 18 * subjectProgress.
    public TodoCycle Advance(TodoCycle cycle) =>
        cycle with { Target = Step + Step * cycle.Progress };
}
