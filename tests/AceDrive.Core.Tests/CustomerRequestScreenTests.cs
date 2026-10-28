using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The request page (PORTPLAN item 11): countdown, quiz grading,
/// passive answers, the forced challenge's self-dismissing read-in, feedback
/// colours and the single exit report.</summary>
public sealed class CustomerRequestScreenTests
{
    private sealed record Harness(
        CustomerRequestScreenViewModel Vm, FakeClock Clock,
        List<CustomerRequestOutcome> Outcomes);

    public CustomerRequestScreenTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static Harness Build(
        CustomerRequest request, int timerSeconds = 19,
        TimeSpan? speedAllowance = null, TimeSpan? speedReadIn = null)
    {
        var clock = new FakeClock();
        var outcomes = new List<CustomerRequestOutcome>();
        var vm = new CustomerRequestScreenViewModel(
            request, "Max Weber", timerSeconds, clock, new VoiceBeat(clock),
            outcomes.Add,
            speedAllowance is { } allowance ? clock.UtcNow + allowance : null,
            speedReadIn);
        return new Harness(vm, clock, outcomes);
    }

    private static CustomerRequest Passive() =>
        new(CustomerRequestKind.Passive, "Could you avoid the potholes?");

    private static CustomerRequest Speed() =>
        new(CustomerRequestKind.SpeedChallenge, "Please get there quickly!");

    private static CustomerRequest Quiz()
    {
        var question = new CarStatQuestion(
            CustomerRequestTestData.Weight, "How heavy is this car?",
            [1400.0, 1500.0, 1600.0], CorrectIndex: 1);
        return new CustomerRequest(CustomerRequestKind.CarQuiz, question.Text, question);
    }

    [Fact]
    public void PassiveRequest_StartsOnTimerWithAcceptAndDecline()
    {
        Harness h = Build(Passive());

        Assert.Equal("TIME REMAINING: 19s", h.Vm.TimeLine);
        Assert.True(h.Vm.TimerVisible);
        Assert.False(h.Vm.DeliveryVisible);
        Assert.False(h.Vm.ShowBack);
        Assert.False(h.Vm.HasFeedback);
        Assert.Equal(2, h.Vm.Options.Count);
        Assert.Equal("Sure sure...", h.Vm.Options[0].Text);
        Assert.Equal("No way, you are asking too much", h.Vm.Options[1].Text);
        Assert.Contains("Sure sure...", h.Vm.VoiceActions.Keys);
        Assert.Contains("No way, you are asking too much", h.Vm.VoiceActions.Keys);
        Assert.DoesNotContain("BACK", h.Vm.VoiceActions.Keys);
        Assert.Empty(h.Outcomes);
    }

    [Fact]
    public void Tick_CountsDownOnWholeSeconds()
    {
        Harness h = Build(Passive());

        h.Clock.Advance(TimeSpan.FromSeconds(0.1));
        h.Vm.Tick(h.Clock.UtcNow);
        Assert.Equal("TIME REMAINING: 19s", h.Vm.TimeLine);

        h.Clock.Advance(TimeSpan.FromSeconds(0.9));
        h.Vm.Tick(h.Clock.UtcNow);
        Assert.Equal("TIME REMAINING: 18s", h.Vm.TimeLine);

        h.Clock.Advance(TimeSpan.FromSeconds(17.5));
        h.Vm.Tick(h.Clock.UtcNow);
        Assert.Equal("TIME REMAINING: 1s", h.Vm.TimeLine);

        h.Clock.Advance(TimeSpan.FromSeconds(0.5));
        h.Vm.Tick(h.Clock.UtcNow);
        Assert.Equal("TIME REMAINING: 0s", h.Vm.TimeLine);
    }

    [Fact]
    public void PassiveTimeout_FinishesAsAnUnansweredAsk()
    {
        // Every request kind runs the countdown; a passive ask that nobody
        // answers still closes itself and reports the timed-out outcome.
        Harness h = Build(Passive());

        h.Clock.Advance(TimeSpan.FromSeconds(19));
        h.Vm.Tick(h.Clock.UtcNow);

        Assert.Equal("No tip then.", h.Vm.Feedback);
        Assert.False(h.Vm.TimerVisible);
        Assert.True(h.Vm.ShowBack);

        h.Vm.BackCommand.Execute(null);

        CustomerRequestOutcome outcome = Assert.Single(h.Outcomes);
        Assert.False(outcome.TipEligible);
        Assert.False(outcome.Answered);
    }

    [Fact]
    public void Timeout_FinishesOnce_ThenBackReportsOnce()
    {
        Harness h = Build(Quiz());

        h.Clock.Advance(TimeSpan.FromSeconds(19));
        h.Vm.Tick(h.Clock.UtcNow);
        h.Vm.Tick(h.Clock.UtcNow);

        Assert.Equal("No tip then.", h.Vm.Feedback);
        Assert.False(h.Vm.FeedbackPositive);
        Assert.True(h.Vm.ShowBack);
        Assert.False(h.Vm.TimerVisible);
        Assert.Empty(h.Vm.Options);
        Assert.Contains("BACK", h.Vm.VoiceActions.Keys);
        Assert.Empty(h.Outcomes);

        h.Vm.BackCommand.Execute(null);
        h.Vm.BackCommand.Execute(null);

        CustomerRequestOutcome outcome = Assert.Single(h.Outcomes);
        Assert.False(outcome.TipEligible);
        Assert.False(outcome.QuizAnsweredCorrectly);
        Assert.False(outcome.Answered);
    }

    [Fact]
    public void QuizCorrect_TealFeedbackAndATip()
    {
        Harness h = Build(Quiz());

        h.Vm.Options[1].Command.Execute(null);

        Assert.Equal("That is impressive, thank you. I will tip you well.", h.Vm.Feedback);
        Assert.True(h.Vm.FeedbackPositive);
        Assert.False(h.Vm.RequestTextVisible);
        // No BACK on the quiz result: it reads for five seconds and reports.
        Assert.False(h.Vm.ShowBack);
        Assert.DoesNotContain("BACK", h.Vm.VoiceActions.Keys);
        Assert.Empty(h.Outcomes);

        h.Clock.Advance(TimeSpan.FromSeconds(4.9));
        h.Vm.Tick(h.Clock.UtcNow);
        Assert.Empty(h.Outcomes);

        h.Clock.Advance(TimeSpan.FromSeconds(0.1));
        h.Vm.Tick(h.Clock.UtcNow);

        CustomerRequestOutcome outcome = Assert.Single(h.Outcomes);
        Assert.True(outcome.TipEligible);
        Assert.True(outcome.QuizAnsweredCorrectly);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void QuizWrong_WhiteFeedbackAndNoTip()
    {
        Harness h = Build(Quiz());

        h.Vm.Options[0].Command.Execute(null);

        Assert.Equal("That can't be right. No tip for you!", h.Vm.Feedback);
        Assert.False(h.Vm.FeedbackPositive);
        Assert.False(h.Vm.RequestTextVisible);
        Assert.False(h.Vm.ShowBack);

        h.Clock.Advance(TimeSpan.FromSeconds(5));
        h.Vm.Tick(h.Clock.UtcNow);

        CustomerRequestOutcome outcome = Assert.Single(h.Outcomes);
        Assert.False(outcome.TipEligible);
        Assert.False(outcome.QuizAnsweredCorrectly);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void QuizOptions_UseNumberButtonsWithTheValueBesideThem()
    {
        Harness h = Build(Quiz());

        Assert.Equal(["ONE", "TWO", "THREE"],
            h.Vm.Options.Select(option => option.ButtonText));
        Assert.Equal(["1400 kg", "1500 kg", "1600 kg"],
            h.Vm.Options.Select(option => option.Text));
        Assert.All(h.Vm.Options, option => Assert.True(option.HasReadout));
        Assert.Contains("1400 kg", h.Vm.VoiceActions.Keys);
        Assert.Contains("ONE", h.Vm.VoiceActions.Keys);

        var correct = h.Vm.Options[1].Command;
        correct.Execute(null);
        // A second tap on a stale row must not grade again or overwrite the
        // outcome; the row is removed from the page already.
        correct.Execute(null);
        h.Clock.Advance(TimeSpan.FromSeconds(5));
        h.Vm.Tick(h.Clock.UtcNow);

        CustomerRequestOutcome outcome = Assert.Single(h.Outcomes);
        Assert.True(outcome.QuizAnsweredCorrectly);
    }

    [Fact]
    public void PassiveAccept_AcceptsWithTheTipLine()
    {
        Harness h = Build(Passive());

        h.Vm.Options[0].Command.Execute(null);

        Assert.Equal("Thank you. I'll tip you when we arrive.", h.Vm.Feedback);
        Assert.True(h.Vm.FeedbackPositive);
        // The tip line replaces the suffix ask; the request text would
        // otherwise still hang above the answer.
        Assert.False(h.Vm.RequestTextVisible);
        h.Vm.BackCommand.Execute(null);

        CustomerRequestOutcome outcome = Assert.Single(h.Outcomes);
        Assert.True(outcome.TipEligible);
        Assert.False(outcome.SpeedChallengeAgreed);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void PassiveDecline_WhiteFeedbackNoTip()
    {
        Harness h = Build(Passive());

        h.Vm.Options[1].Command.Execute(null);

        Assert.Equal("Fair enough. No tip then.", h.Vm.Feedback);
        Assert.False(h.Vm.FeedbackPositive);
        Assert.False(h.Vm.RequestTextVisible);
        h.Vm.BackCommand.Execute(null);

        CustomerRequestOutcome outcome = Assert.Single(h.Outcomes);
        Assert.False(outcome.TipEligible);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void SpeedChallenge_IsAReadInWithoutAnswerOrButton()
    {
        Harness h = Build(
            Speed(), speedAllowance: TimeSpan.FromSeconds(90),
            speedReadIn: TimeSpan.FromSeconds(30));

        Assert.True(h.Vm.IsSpeedChallenge);
        Assert.False(h.Vm.ShowBack);
        Assert.False(h.Vm.TimerVisible);
        Assert.True(h.Vm.DeliveryVisible);
        Assert.True(h.Vm.RequestTextVisible);
        Assert.Empty(h.Vm.Options);
        // No button and no answer row: nothing to tap, nothing to speak.
        Assert.Empty(h.Vm.VoiceActions);
        Assert.Equal("DELIVER WITHIN 1:30", h.Vm.DeliveryLine);

        h.Clock.Advance(TimeSpan.FromSeconds(5.2));
        h.Vm.Tick(h.Clock.UtcNow);
        Assert.Equal("DELIVER WITHIN 1:25", h.Vm.DeliveryLine);
        Assert.Empty(h.Outcomes);
    }

    [Fact]
    public void SpeedChallenge_LateDeadlinePaintsTheWarningLine()
    {
        // The page is a read-in; a deadline that passes while it is up paints
        // the warning line without ending the page early.
        Harness h = Build(
            Speed(), speedAllowance: TimeSpan.FromSeconds(10),
            speedReadIn: TimeSpan.FromSeconds(60));

        h.Clock.Advance(TimeSpan.FromSeconds(11));
        h.Vm.Tick(h.Clock.UtcNow);

        Assert.True(h.Vm.SpeedChallengeLate);
        Assert.Equal("TOO LATE", h.Vm.DeliveryLine);
        Assert.False(h.Vm.ShowBack);
        Assert.Empty(h.Outcomes);
    }

    [Fact]
    public void SpeedChallenge_ReadInDismissesItAndReportsOnce()
    {
        // The forced page has nothing to answer and no button: its read-in
        // running out reports the agreed outcome, so the host restores the
        // task text while the delivery clock keeps running.
        Harness h = Build(
            Speed(), speedAllowance: TimeSpan.FromSeconds(300),
            speedReadIn: TimeSpan.FromSeconds(3));

        h.Clock.Advance(TimeSpan.FromSeconds(3));
        h.Vm.Tick(h.Clock.UtcNow);
        h.Vm.Tick(h.Clock.UtcNow);
        h.Vm.BackCommand.Execute(null);

        CustomerRequestOutcome outcome = Assert.Single(h.Outcomes);
        Assert.True(outcome.SpeedChallengeAgreed);
        Assert.True(outcome.TipEligible);
        Assert.True(outcome.Answered);
    }

    [Fact]
    public void BackBeforeFinish_ReportsNothing()
    {
        Harness h = Build(Passive());

        h.Vm.BackCommand.Execute(null);

        Assert.Empty(h.Outcomes);
    }
}
