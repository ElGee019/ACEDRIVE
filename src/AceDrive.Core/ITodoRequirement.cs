// One ToDo subject's rule (PORTPLAN item 46, port of Unity
// Todo/IRequirementCheck + ITodoEvaluator). A new subject is a new
// implementation registered in TodoRequirements, never a branch in the ledger.
namespace AceDrive.Core;

/// <summary>
/// What one subject requires, how it displays, and how its target advances.
/// Pure: the facts record is the whole world a requirement may read.
/// </summary>
public interface ITodoRequirement
{
    /// <summary>The subject id this rule answers for (<see cref="TodoSubject"/>).</summary>
    string Subject { get; }

    /// <summary>
    /// True for a one-time visit: the subject retires after its popup row
    /// instead of cycling to a new target (Unity IsOneTime).
    /// </summary>
    bool IsOneTime { get; }

    /// <summary>The first target a fresh career shows.</summary>
    double InitialTarget { get; }

    /// <summary>The resx key the tip line formats.</summary>
    string TipKey { get; }

    /// <summary>
    /// The POI type whose name fills the tip's {0} ("Enter {0}."), or null for
    /// a subject with authored copy (Max's grocery, the bank visit).
    /// </summary>
    string? PoiType { get; }

    /// <summary>
    /// The authored sentence used when no door of <see cref="PoiType"/> is
    /// known yet (Unity GetTipText's fallback), or null for progressive copy.
    /// </summary>
    string? FallbackKey { get; }

    /// <summary>The career reading the tip shows next to the target.</summary>
    double Current(TodoFacts facts);

    /// <summary>True when the career already meets the target.</summary>
    bool IsMet(TodoFacts facts, double target);

    /// <summary>The next target after one completion (Unity CycleThresholdOnce).</summary>
    TodoCycle Advance(TodoCycle cycle);
}
