// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity CustomerRequestScreen consequences).
namespace AceDrive.Core;

/// <summary>
/// What answering a request decided. The host copies these onto the job; the
/// receipt tips on <see cref="TipEligible"/> OR
/// <see cref="QuizAnsweredCorrectly"/> (Unity's rules: a police fine clears
/// the former but never a correct quiz tip). The driver-rating deltas ride
/// this outcome at the host (item 06): a right answer lifts off-the-line, a
/// wrong answer or a timeout drops it. <see cref="Declined"/> is what the host
/// reads to award the assertivity stat the Cento Miglia grows from (item 49);
/// its size is balance and lives in CustomerRequestTable.
/// </summary>
public sealed record CustomerRequestOutcome(
    bool TipEligible,
    bool QuizAnsweredCorrectly,
    bool SpeedChallengeAgreed,
    bool Answered,
    bool Declined = false)
{
    public static readonly CustomerRequestOutcome None = new(false, false, false, false);
}
