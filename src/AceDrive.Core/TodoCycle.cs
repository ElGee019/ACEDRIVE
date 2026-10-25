// One threshold advance of a progressive ToDo (PORTPLAN item 46, port of
// Unity ITodoEvaluator.CycleThresholdOnce). Pure value: the ledger owns the
// storage, the requirement only computes the next step. Progress is already
// incremented when the requirement sees it (Unity increments subjectProgress
// before calling the evaluator), so the curves below read it the same way.
namespace AceDrive.Core;

/// <summary>
/// The state one threshold cycle starts from: the current target, how many
/// times the subject already cycled, and the bank-balance subject's interest
/// side value (Unity's bankInterests list).
/// </summary>
public readonly record struct TodoCycle(double Target, int Progress, int BankInterest);
