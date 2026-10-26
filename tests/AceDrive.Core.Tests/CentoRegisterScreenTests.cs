using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The registration page runs headless (PORTPLAN 49, Unity
/// RegisterScreen): which digits the keypad paints as selected, the selected
/// number above the keys, and the digits the mic can pick.</summary>
public sealed class CentoRegisterScreenTests
{
    // The spoken digits the page promises to answer, in keypad order.
    private static readonly string[] Words =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine",
    ];

    private static CentoRegisterScreenViewModel Create(int registration = 12)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var check = new CentoRegistrationCheck(registration);
        return new CentoRegisterScreenViewModel(
            check, () => { }, () => { }, new VoiceBeat(new FakeClock()));
    }

    private static CentoRegisterKey Key(CentoRegisterScreenViewModel page, int digit) =>
        Assert.Single(page.Keys, key => key.Digit == digit);

    [Fact]
    public void SelectingADigit_PaintsThatKeyAndTheSelectedNumber()
    {
        CentoRegisterScreenViewModel page = Create();

        page.DigitCommand.Execute(1);

        Assert.True(Key(page, 1).Selected);
        Assert.False(Key(page, 2).Selected);
        Assert.Equal("SELECTED: 1", page.SelectedLine);
    }

    [Fact]
    public void JudgedNumber_KeepsItsKeysSelectedUntilTheNextDigit()
    {
        CentoRegisterScreenViewModel page = Create();

        page.DigitCommand.Execute(1);
        page.DigitCommand.Execute(3);   // wrong: the page waits for a retry

        Assert.True(Key(page, 1).Selected);
        Assert.True(Key(page, 3).Selected);
        Assert.Equal("SELECTED: 1 3", page.SelectedLine);

        page.DigitCommand.Execute(1);   // the retry starts from an empty buffer

        Assert.True(Key(page, 1).Selected);
        Assert.False(Key(page, 3).Selected);
        Assert.Equal("SELECTED: 1", page.SelectedLine);
    }

    [Fact]
    public void AcceptedNumber_StaysSelectedAndOffersContinue()
    {
        CentoRegisterScreenViewModel page = Create();

        page.DigitCommand.Execute(1);
        page.DigitCommand.Execute(2);

        Assert.True(page.ShowContinue);
        Assert.False(page.CanType);
        Assert.True(Key(page, 1).Selected);
        Assert.True(Key(page, 2).Selected);
        Assert.Equal("SELECTED: 1 2", page.SelectedLine);
    }

    [Fact]
    public void SingleDigitNumber_ShowsTheSelectedDigitAndSolves()
    {
        // A first-tier number is one digit, so it is judged the instant the key
        // lands: the page must still show the digit that was picked.
        CentoRegisterScreenViewModel page = Create(registration: 7);

        page.DigitCommand.Execute(7);

        Assert.True(page.ShowContinue);
        Assert.True(Key(page, 7).Selected);
        Assert.Equal("SELECTED: 7", page.SelectedLine);
    }

    [Fact]
    public void EveryDigit_AnswersItsSpokenWordAndItsNumeral()
    {
        for (int digit = 0; digit <= 9; digit++)
        {
            string word = Words[digit];
            CentoRegisterScreenViewModel byWord = Create();
            byWord.VoiceActions[word].Invoke();
            Assert.True(Key(byWord, digit).Selected, $"spoken \"{word}\"");
            Assert.Equal($"SELECTED: {digit}", byWord.SelectedLine);

            string numeral = digit.ToString(CultureInfo.InvariantCulture);
            CentoRegisterScreenViewModel byNumeral = Create();
            byNumeral.VoiceActions[numeral].Invoke();
            Assert.True(Key(byNumeral, digit).Selected, $"spoken \"{numeral}\"");
        }
    }

    [Fact]
    public void ASpokenDigit_AnswersWithOneSharedDelegate()
    {
        // The router's repeat mute compares delegates: the word and the numeral
        // must be the same tap, or one utterance could enter the digit twice.
        CentoRegisterScreenViewModel page = Create();

        Assert.Same(page.VoiceActions["one"], page.VoiceActions["1"]);
    }

    [Fact]
    public void SolvedEntry_OffersContinueByVoiceAndDropsTheKeypad()
    {
        CentoRegisterScreenViewModel page = Create();

        page.DigitCommand.Execute(1);
        page.DigitCommand.Execute(2);

        Assert.True(page.VoiceActions.ContainsKey("CONTINUE"));
        Assert.False(page.VoiceActions.ContainsKey("one"));
        Assert.False(page.VoiceActions.ContainsKey("1"));
    }

    [Fact]
    public void ClosedEntry_DropsTheKeypadAndKeepsTheRejectedNumber()
    {
        CentoRegisterScreenViewModel page = Create();

        page.DigitCommand.Execute(1);
        page.DigitCommand.Execute(3);   // first wrong number
        page.DigitCommand.Execute(1);
        page.DigitCommand.Execute(4);   // second wrong number: closed

        Assert.True(page.IsClosed);
        Assert.False(page.VoiceActions.ContainsKey("one"));
        Assert.False(page.VoiceActions.ContainsKey("1"));
        Assert.Equal("SELECTED: 1 4", page.SelectedLine);
    }
}
