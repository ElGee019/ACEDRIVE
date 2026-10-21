using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>Every request ending maps to one outcome (PORTPLAN item 11).</summary>
public sealed class CustomerRequestGraderTests
{
    private static readonly CustomerRequest Passive =
        new(CustomerRequestKind.Passive, "Anything?");

    private static readonly CustomerRequest Speed =
        new(CustomerRequestKind.SpeedChallenge, "Quickly!");

    private static readonly CarStatQuestion QuizQuestion =
        new(CustomerRequestTestData.Weight, "How heavy?",
            [1400.0, 1500.0, 1600.0], CorrectIndex: 0);

    private static readonly CustomerRequest Quiz =
        new(CustomerRequestKind.CarQuiz, "How heavy?", QuizQuestion);

    [Fact]
    public void PassiveAccept_Tips()
    {
        CustomerRequestOutcome outcome = CustomerRequestGrader.Accept(Passive);

        Assert.True(outcome.TipEligible);
        Assert.False(outcome.SpeedChallengeAgreed);
        Assert.False(outcome.QuizAnsweredCorrectly);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void PassiveDecline_NoTip()
    {
        CustomerRequestOutcome outcome = CustomerRequestGrader.Decline(Passive);

        Assert.False(outcome.TipEligible);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void SpeedAccept_AgreesAndTips()
    {
        CustomerRequestOutcome outcome = CustomerRequestGrader.Accept(Speed);

        Assert.True(outcome.TipEligible);
        Assert.True(outcome.SpeedChallengeAgreed);
        Assert.False(outcome.QuizAnsweredCorrectly);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void SpeedDecline_NoTipNoAgreement()
    {
        CustomerRequestOutcome outcome = CustomerRequestGrader.Decline(Speed);

        Assert.False(outcome.TipEligible);
        Assert.False(outcome.SpeedChallengeAgreed);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void QuizCorrect_TipsAndMarksTheQuiz()
    {
        CustomerRequestOutcome outcome = CustomerRequestGrader.Answer(Quiz, index: 0);

        Assert.True(outcome.TipEligible);
        Assert.True(outcome.QuizAnsweredCorrectly);
        Assert.False(outcome.SpeedChallengeAgreed);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void QuizWrong_NoTip()
    {
        CustomerRequestOutcome outcome = CustomerRequestGrader.Answer(Quiz, index: 1);

        Assert.False(outcome.TipEligible);
        Assert.False(outcome.QuizAnsweredCorrectly);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void QuizOutOfRange_NoTip()
    {
        CustomerRequestOutcome outcome = CustomerRequestGrader.Answer(Quiz, index: 99);

        Assert.False(outcome.TipEligible);
        Assert.False(outcome.QuizAnsweredCorrectly);
    }

    [Fact]
    public void Timeout_NoTipAndNotAnswered()
    {
        CustomerRequestOutcome outcome = CustomerRequestGrader.Timeout(Quiz);

        Assert.Equal(CustomerRequestOutcome.None, outcome);
    }

    [Fact]
    public void AcceptOrDeclineOnAQuiz_IsNotAValidEnding()
    {
        Assert.Equal(CustomerRequestOutcome.None, CustomerRequestGrader.Accept(Quiz));
        Assert.Equal(CustomerRequestOutcome.None, CustomerRequestGrader.Decline(Quiz));
    }
}
