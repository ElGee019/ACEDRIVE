// The persisted ToDo ledger (PORTPLAN item 46, port of Unity
// TodoPersistedState). Unity stores fixed-size index arrays and patches them
// on every schema change; this port keys every map by subject id instead, so
// adding or removing a subject needs no migration — an unknown id drops out
// and a new one starts at its requirement's defaults.
namespace AceDrive.Core;

/// <summary>
/// A whole ToDo career: the four visible slots, the rotation queue, the
/// per-subject targets/progress/interest, the completion flags and the
/// captured completion tips.
/// </summary>
public sealed record TodoLedgerState(
    IReadOnlyList<TodoSlotState> Visible,
    IReadOnlyList<string> Queue,
    IReadOnlyDictionary<string, double> Targets,
    IReadOnlyDictionary<string, int> Progress,
    IReadOnlyDictionary<string, int> BankInterests,
    IReadOnlyList<string> Awaiting,
    IReadOnlyList<string> Shown,
    IReadOnlyList<string> Activated,
    IReadOnlyDictionary<string, TodoTipSnapshot> CompletedTips);
