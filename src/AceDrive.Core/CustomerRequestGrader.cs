// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity CustomerRequestScreen handlers).
namespace AceDrive.Core;

/// <summary>
/// The four ways a request can end: accepted, declined, answered (quiz) or
/// timed out. Pure data in, pure outcome out, so the page and the host tests
/// share one rule set.
/// </summary>
public static class CustomerRequestGrader
{
    public static CustomerRequestOutcome Accept(CustomerRequest request) =>
        request.Kind == CustomerRequestKind.Passive
            ? new CustomerRequestOutcome(
                TipEligible: true,
                QuizAnsweredCorrectly: false,
                SpeedChallengeAgreed: false,
                Answered: true)
            : request.Kind == CustomerRequestKind.SpeedChallenge
                ? new CustomerRequestOutcome(
                    TipEligible: true,
                    QuizAnsweredCorrectly: false,
                    SpeedChallengeAgreed: true,
                    Answered: true)
                : CustomerRequestOutcome.None;

    public static CustomerRequestOutcome Decline(CustomerRequest request) =>
        request.Kind is CustomerRequestKind.Passive or CustomerRequestKind.SpeedChallenge
            ? new CustomerRequestOutcome(
                TipEligible: false,
                QuizAnsweredCorrectly: false,
                SpeedChallengeAgreed: false,
                Answered: true)
            : CustomerRequestOutcome.None;

    public static CustomerRequestOutcome Answer(CustomerRequest request, int index)
    {
        if (request.Kind != CustomerRequestKind.CarQuiz || request.Quiz is not { } quiz)
        {
            return CustomerRequestOutcome.None;
        }

        bool correct = index == quiz.CorrectIndex;
        return new CustomerRequestOutcome(
            TipEligible: correct,
            QuizAnsweredCorrectly: correct,
            SpeedChallengeAgreed: false,
            Answered: true);
    }

    public static CustomerRequestOutcome Timeout(CustomerRequest _) =>
        new(
            TipEligible: false,
            QuizAnsweredCorrectly: false,
            SpeedChallengeAgreed: false,
            Answered: false);
}
