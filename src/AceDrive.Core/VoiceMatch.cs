// Pure voice-text matching. No mic / STT / UI references.
// Ported from the evo-companion wake-word spec: "hey {brand}" shows the
// overlay, and a visible screen exposes its button labels for whole-word
// matching (longest label wins). Deterministic and fully unit-tested.
using System.Text.RegularExpressions;

namespace AceDrive.Core;

/// <summary>Wake-word + spoken-button matching over STT text fragments.</summary>
public static class VoiceMatch
{
    private static readonly Regex NonWord = new("[^a-z0-9]+", RegexOptions.Compiled);

    /// <summary>Lowercase, non-alphanumerics to single spaces, trimmed.</summary>
    public static string Normalize(string text) =>
        NonWord.Replace(text.ToLowerInvariant(), " ").Trim();

    /// <summary>
    /// True when text contains "hey &lt;brand&gt;" (filler words allowed around
    /// it). Aliases cover systematic mishearings: recognizer vocabularies favor
    /// common words, so "hey lancia" often arrives as "hey lancer".
    /// </summary>
    public static bool IsWake(string text, string brand, IReadOnlyList<string>? aliases = null)
    {
        string[] words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return false;
        }

        List<string[]> targets = new() { Normalize(brand).Split(' ', StringSplitOptions.RemoveEmptyEntries) };
        if (aliases is not null)
        {
            foreach (string alias in aliases)
            {
                targets.Add(Normalize(alias).Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        targets.RemoveAll(t => t.Length == 0);
        if (targets.Count == 0)
        {
            return false;
        }

        for (int i = 0; i < words.Length; i++)
        {
            if (words[i] != "hey")
            {
                continue;
            }

            foreach (string[] target in targets)
            {
                if (i + 1 + target.Length <= words.Length &&
                    words.AsSpan(i + 1, target.Length).SequenceEqual(target))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Best button label appearing as whole words in text; longest wins, first
    /// wins ties. Null when nothing matches. The caller maps the label to its
    /// action, so Core stays free of UI delegates.
    /// </summary>
    public static string? MatchLabel(string text, IEnumerable<string> labels)
    {
        string[] words = Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string? best = null;
        int bestLen = 0;
        foreach (string label in labels)
        {
            string[] parts = Normalize(label).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= bestLen || parts.Length > words.Length)
            {
                continue;
            }

            for (int i = 0; i <= words.Length - parts.Length; i++)
            {
                if (words.AsSpan(i, parts.Length).SequenceEqual(parts))
                {
                    best = label;
                    bestLen = parts.Length;
                    break;
                }
            }
        }

        return best;
    }
}
