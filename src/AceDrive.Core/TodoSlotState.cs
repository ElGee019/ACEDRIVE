// One visible ToDo slot (PORTPLAN item 46, port of Unity Todo/TodoSlot): the
// subject it shows and the job counter that rotates it out after six settled
// jobs. Turns is the only per-slot state, exactly like Unity's turnsCounter.
namespace AceDrive.Core;

/// <summary>One of the four visible tips: its subject and its rotation counter.</summary>
public sealed record TodoSlotState(string SubjectId, int Turns);
