// A follow-up tip's gate (PORTPLAN item 46): the subject whose completion
// reveals this tip, so a tutorial step can follow the one before it. A
// separate interface on purpose (ISP): only the gated tips carry a gate, and
// the ledger reads it without widening ITodoRequirement for every other
// requirement to answer.
namespace AceDrive.Core;

/// <summary>A tip that stays out of the list until another subject completes.</summary>
public interface IUnlockedTodo
{
    /// <summary>The subject whose completion reveals this tip.</summary>
    string UnlockedBy { get; }
}
