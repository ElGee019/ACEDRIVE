// Cento Miglia registration verification (PORTPLAN item 49, port of Unity
// MilleMigliaManager.SubmitRegNumberAttempt + RegAttemptResult). The player
// reads the number on the event page, drives to the start, and types it on the
// register screen: a wrong entry costs one attempt, the second closes the
// entry. Pure rules, so the screen only paints what this answers.
namespace AceDrive.Core;

/// <summary>
/// One digit submission's outcome. Mirrors Unity's <c>RegAttemptResult</c>:
/// a pending digit is neither right nor wrong yet, and the closed state is
/// final for the entry.
/// </summary>
public enum CentoRegistrationResult
{
    /// <summary>The digit was accepted into the buffer; the number is not complete yet.</summary>
    DigitPending,

    /// <summary>The buffered digits match the entry's registration.</summary>
    Accepted,

    /// <summary>The buffered digits were wrong; the entry survives (attempt left).</summary>
    WrongAttempt,

    /// <summary>The error limit was reached: the entry is refused.</summary>
    Closed,
}
