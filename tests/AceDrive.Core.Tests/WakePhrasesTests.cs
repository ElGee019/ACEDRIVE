using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The wake decoder's phrase list (voice dial-in, 2026-10-30): every brand the
/// content knows becomes "hey {brand}", every logged garble of one becomes
/// "hey {garble}", and bare aliases stay out â€” a one-word phrase is what a
/// grammar decoder emits from engine noise, not a wake.
/// </summary>
public sealed class WakePhrasesTests
{
    private static readonly OnboardingContent Content = new(
        ["Honda", "Alfa Romeo"],
        new Dictionary<string, string[]>
        {
            ["audi"] = ["odie"],
            ["datsun"] = ["nissan", "that's fun"],
            ["honda"] = ["Honda", "hunter"],
        },
        [],
        [],
        [],
        [],
        [],
        new Dictionary<string, string>());

    [Fact]
    public void From_Brands_BecomeHeyPhrases()
    {
        IReadOnlyList<string> phrases = WakePhrases.From(Content);

        Assert.Contains("hey honda", phrases);
        // A two-word brand stays one phrase: the decoder may not split it.
        Assert.Contains("hey alfa romeo", phrases);
    }

    [Fact]
    public void From_Aliases_BecomeHeyPhrases_MultiWordOnesIncluded()
    {
        IReadOnlyList<string> phrases = WakePhrases.From(Content);

        Assert.Contains("hey odie", phrases);
        Assert.Contains("hey nissan", phrases);
        Assert.Contains("hey hunter", phrases);
        // Normalized like every other voice table, so the apostrophe is gone.
        Assert.Contains("hey that s fun", phrases);
    }

    [Fact]
    public void From_NeverShipsABareAlias()
    {
        IReadOnlyList<string> phrases = WakePhrases.From(Content);

        Assert.DoesNotContain("nissan", phrases);
        Assert.DoesNotContain("odie", phrases);
    }

    [Fact]
    public void From_DeduplicatesABrandSpelledThroughItsOwnAlias()
    {
        IReadOnlyList<string> phrases = WakePhrases.From(Content);

        Assert.Single(phrases, phrase => phrase == "hey honda");
    }

    [Fact]
    public void From_EmptyContent_HasNoPhrases()
    {
        var empty = new OnboardingContent(
            [], new Dictionary<string, string[]>(), [], [], [], [], []);

        Assert.Empty(WakePhrases.From(empty));
    }

    [Fact]
    public void From_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => WakePhrases.From(null!));
    }
}
