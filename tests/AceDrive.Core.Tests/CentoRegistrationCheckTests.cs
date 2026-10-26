using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The Cento Miglia registration check (PORTPLAN 49, Unity
/// MilleMigliaManager.SubmitRegNumberAttempt): digit buffering per tier length,
/// one wrong attempt left, the second wrong number closing the entry.</summary>
public sealed class CentoRegistrationCheckTests
{
    [Fact]
    public void SingleDigitNumber_IsAcceptedAtOnce()
    {
        var check = new CentoRegistrationCheck(7);
        Assert.Equal(1, check.DigitCount);
        Assert.Equal(CentoRegistrationResult.Accepted, check.Submit(7));
        Assert.True(check.IsSolved);
        Assert.False(check.IsClosed);
    }

    [Fact]
    public void Zero_IsAOneDigitNumber()
    {
        // Unity's DigitCountOf(0) is 1: the first club may draw 0.
        var check = new CentoRegistrationCheck(0);
        Assert.Equal(1, check.DigitCount);
        Assert.Equal(CentoRegistrationResult.Accepted, check.Submit(0));
    }

    [Fact]
    public void MultiDigitNumber_WaitsForEveryDigit()
    {
        var check = new CentoRegistrationCheck(12);
        Assert.Equal(2, check.DigitCount);
        Assert.Equal(CentoRegistrationResult.DigitPending, check.Submit(1));
        Assert.Equal([1], check.EnteredDigits);
        Assert.Equal(CentoRegistrationResult.Accepted, check.Submit(2));
        // The judged number stays up: the page paints the keypad selection
        // from this buffer, so clearing it on the last digit would blank the
        // number the player just picked.
        Assert.Equal([1, 2], check.EnteredDigits);
    }

    [Fact]
    public void WrongNumber_CostsOneAttemptAndStartsOver()
    {
        var check = new CentoRegistrationCheck(12);
        Assert.Equal(CentoRegistrationResult.DigitPending, check.Submit(1));
        Assert.Equal(CentoRegistrationResult.WrongAttempt, check.Submit(3));
        Assert.Equal(1, check.Attempts);
        // The rejected number stays up until the next digit starts the retry.
        Assert.Equal([1, 3], check.EnteredDigits);
        Assert.False(check.IsClosed);

        // The right number still works with one attempt left.
        Assert.Equal(CentoRegistrationResult.DigitPending, check.Submit(1));
        Assert.Equal([1], check.EnteredDigits);
        Assert.Equal(CentoRegistrationResult.Accepted, check.Submit(2));
    }

    [Fact]
    public void SecondWrongNumber_ClosesTheEntryForGood()
    {
        var check = new CentoRegistrationCheck(12);
        Assert.Equal(CentoRegistrationResult.DigitPending, check.Submit(1));
        Assert.Equal(CentoRegistrationResult.WrongAttempt, check.Submit(3));
        Assert.Equal(1, check.Attempts);
        Assert.Equal(CentoRegistrationResult.DigitPending, check.Submit(1));
        Assert.Equal(CentoRegistrationResult.Closed, check.Submit(4));
        Assert.True(check.IsClosed);
        Assert.False(check.IsSolved);

        // A closed entry never reopens, not even with the right number, and
        // the number that closed it stays on the page.
        Assert.Equal(CentoRegistrationResult.Closed, check.Submit(1));
        Assert.Equal(CentoRegistrationResult.Closed, check.Submit(2));
        Assert.Equal([1, 4], check.EnteredDigits);
    }

    [Fact]
    public void DigitOutsideTheKeypad_IsRejected()
    {
        var check = new CentoRegistrationCheck(7);
        Assert.Throws<ArgumentOutOfRangeException>(() => check.Submit(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => check.Submit(10));
    }
}
