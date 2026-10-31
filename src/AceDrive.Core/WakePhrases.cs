// The wake-word decoder's phrase list (voice dial-in, 2026-10-30). Vosk's
// constrained decoder can only emit the phrases it is handed, so engine noise
// and room audio stop arriving as "the be some the have nice ... nissan"
// while the panel is down: the recognizer has nothing else it may say.
namespace AceDrive.Core;

/// <summary>
/// The phrases a wake-only decoder may emit: "hey {brand}" for every brand the
/// content knows, plus every logged mishearing of one ("hey odie", "hey
/// dotson", ...), so a brand the English model cannot spell still wakes. Bare
/// aliases stay out: a one-word phrase is exactly what a grammar decoder likes
/// to emit from engine noise, and the matcher's solo-alias door still covers
/// the full-decoder fallback (see <see cref="VoiceMatch.IsWake"/>).
/// </summary>
public static class WakePhrases
{
    private const string Interjection = "hey";

    /// <summary>Every wake phrase, normalized, deduplicated and sorted.</summary>
    public static IReadOnlyList<string> From(OnboardingContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var phrases = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string brand in content.Brands)
        {
            Add(phrases, brand);
        }

        foreach (KeyValuePair<string, string[]> entry in content.Aliases)
        {
            Add(phrases, entry.Key);
            foreach (string alias in entry.Value)
            {
                Add(phrases, alias);
            }
        }

        return [.. phrases];
    }

    private static void Add(SortedSet<string> phrases, string heard)
    {
        string run = VoiceMatch.Normalize(heard);
        if (run.Length > 0)
        {
            phrases.Add($"{Interjection} {run}");
        }
    }
}
