// A follow-up ToDo (PORTPLAN item 46): an authored one-time tip that waits
// for its gate subject to complete before it may take a slot. It composes the
// one-time behaviour of VisitTodoRequirement and adds the gate, so the two
// rules stay one implementation each (composition over copy-paste).
namespace AceDrive.Core;

/// <summary>A gated one-time tip: once its gate completes, the tip shows.</summary>
public sealed class FollowUpTodoRequirement : ITodoRequirement, IUnlockedTodo
{
    private readonly VisitTodoRequirement _inner;

    public FollowUpTodoRequirement(
        string subject, string unlockedBy, string tipKey, Func<TodoFacts, bool> done)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(unlockedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipKey);
        ArgumentNullException.ThrowIfNull(done);
        // The tip is authored copy with no door to name, so its fallback key
        // is its own key (the line reads the same before and after the gate).
        _inner = new VisitTodoRequirement(subject, tipKey, tipKey, done);
        UnlockedBy = unlockedBy;
    }

    public string UnlockedBy { get; }
    public string Subject => _inner.Subject;
    public bool IsOneTime => _inner.IsOneTime;
    public double InitialTarget => _inner.InitialTarget;
    public string TipKey => _inner.TipKey;
    public string? PoiType => _inner.PoiType;
    public string? FallbackKey => _inner.FallbackKey;

    public double Current(TodoFacts facts) => _inner.Current(facts);
    public bool IsMet(TodoFacts facts, double target) => _inner.IsMet(facts, target);
    public TodoCycle Advance(TodoCycle cycle) => _inner.Advance(cycle);
}
