namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Spoken words the registration keypad answers to. The buttons print digits,
/// so these words live in code rather than in the resx: the Vosk model is
/// English, so a digit has to answer its English word whatever language the
/// page prints (the companion button keywords' rule). Every digit lists its
/// word and its numeral, because the model returns either ("three" one
/// utterance, "3" the next). The confusion table stays the place for the
/// garbles the logs actually produce, exactly as it is for every other button.
/// </summary>
internal static class CentoRegisterVoice
{
    private static readonly string[][] DigitKeywords =
    [
        ["zero", "0"],
        ["one", "1"],
        ["two", "2"],
        ["three", "3"],
        ["four", "4"],
        ["five", "5"],
        ["six", "6"],
        ["seven", "7"],
        ["eight", "8"],
        ["nine", "9"],
    ];

    /// <summary>The words that select one keypad digit; empty off the keypad.</summary>
    public static IReadOnlyList<string> KeywordsFor(int digit) =>
        digit >= 0 && digit < DigitKeywords.Length ? DigitKeywords[digit] : [];
}
