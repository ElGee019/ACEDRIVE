// One one-time visit ToDo (PORTPLAN item 46, Unity SingleActionTodoEvaluator):
// the requirement is a career flag, the tip names the door when one is known
// and falls back to the authored sentence otherwise.
namespace AceDrive.Core;

public sealed class VisitTodoRequirement : ITodoRequirement
{
    private readonly Func<TodoFacts, bool> _done;

    public VisitTodoRequirement(
        string subject, string tipKey, string fallbackKey,
        Func<TodoFacts, bool> done, string? poiType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackKey);
        ArgumentNullException.ThrowIfNull(done);
        Subject = subject;
        TipKey = tipKey;
        FallbackKey = fallbackKey;
        _done = done;
        PoiType = poiType;
    }

    public string Subject { get; }
    public bool IsOneTime => true;
    public double InitialTarget => 0.0;
    public string TipKey { get; }
    public string? PoiType { get; }
    public string? FallbackKey { get; }

    public double Current(TodoFacts facts) => _done(facts) ? 1.0 : 0.0;

    public bool IsMet(TodoFacts facts, double target) => _done(facts);

    // One-time subjects never cycle: a retired subject is never re-flagged.
    public TodoCycle Advance(TodoCycle cycle) => cycle;
}
