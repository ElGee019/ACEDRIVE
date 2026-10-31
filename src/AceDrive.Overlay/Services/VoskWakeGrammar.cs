using System.Text.Json;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Builds the JSON grammar a Vosk recognizer can be constrained to: the
/// wake phrases plus <c>[unk]</c>, so everything else the mic hears decodes to
/// a single unknown token instead of a sentence. Vosk's grammar decoder is the
/// tool this needs â€” it can only say what it was given, which is why "hey
/// datsun" survives engine noise where the full vocabulary turned the same
/// audio into "the be some the have nice of the have nissan".
///
/// A phrase whose words are not all in the model's vocabulary is dropped
/// whole: Kaldi skips an unknown word silently, which would leave a bare "hey"
/// in the grammar and turn any interjection into a wake.
/// </summary>
public static class VoskWakeGrammar
{
    // Kaldi's out-of-grammar token: speech the phrases do not cover decodes to
    // this and matches nothing in VoiceMatch.
    internal const string Unknown = "[unk]";

    /// <summary>The grammar, or null when no phrase is pronounceable.</summary>
    public static WakeGrammar? Build(IReadOnlyList<string> phrases, Func<string, bool> known)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        ArgumentNullException.ThrowIfNull(known);

        List<string> usable = [];
        foreach (string phrase in phrases)
        {
            string[] words = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0 && Array.TrueForAll(words, word => known(word)))
            {
                usable.Add(phrase);
            }
        }

        if (usable.Count == 0)
        {
            return null;
        }

        int kept = usable.Count;
        usable.Add(Unknown);
        return new WakeGrammar(
            JsonSerializer.Serialize(usable), kept, phrases.Count - kept);
    }
}
