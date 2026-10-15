// Pure voice-text matching. No mic / STT / UI references.
// Ported from the evo-companion wake-word spec: "hey {brand}" shows the
// overlay, and a visible screen exposes its button labels for whole-word
// matching (whole labels beat leading slices, longest wins). Deterministic
// and fully unit-tested.
using System.Text.RegularExpressions;

namespace AceDrive.Core;

/// <summary>
/// Wake-word + spoken-button matching over STT text fragments, tuned for
/// how the English Vosk model hears non-native speakers: misheard
/// interjections get an exact brand, misheard brands get one slip, and
/// buttons also answer to their leading words ("open new" fires the button
/// that prints "OPEN NEW CAREER"). Buttons additionally answer to logged
/// whole-word mishearings resolved through a data table ("open your
/// career" for "open new career") without ever losing the exact match.
/// </summary>
public static class VoiceMatch
{
    private static readonly Regex NonWord = new("[^a-z0-9]+", RegexOptions.Compiled);

    // Scores that make "answered as the whole label" outrank any
    // leading-slice answer, independent of registration order.
    private const int WholeLabelBonus = 100;

    private const string Interjection = "hey";
    private const int InterjectionSlack = 2;

    // Stop-words that sit within InterjectionSlack of "hey" ("the"=2,
    // "they"=1) but are far too common in free-running transcripts to
    // double as wake interjections. The logged 2026-10-14 wall of bare
    // "the"s is exactly this floor.
    private static readonly HashSet<string> CommonWords =
    [
        "the", "they", "there", "their", "here", "then", "them", "this",
        "that", "with", "from", "for", "and", "not", "but", "what", "when",
        "where", "how", "who", "you", "your", "our",
    ];

    // The two stop-words the logs actually caught standing where "hey"
    // was said ("the a lotus", 2026-10-14 16:04:16 "…massive the lotus").
    // They act as a clipped interjection, but only for the brand's exact
    // word run (see IsWake). Other CommonWords sit within InterjectionSlack
    // of "hey" by Damerau ("when", "what") yet legitimately open real
    // sentences about the car — "when lotus launched the Emira" must stay
    // chatter, so the door lists its evidence instead of measuring.
    private static readonly HashSet<string> ClippedHey = ["the", "they"];

    /// <summary>Lowercase, non-alphanumerics to single spaces, trimmed.</summary>
    public static string Normalize(string text) =>
        NonWord.Replace(text.ToLowerInvariant(), " ").Trim();

    /// <summary>
    /// True when text says "hey &lt;brand&gt;" (filler words allowed around
    /// it). Aliases cover systematic mishearings: recognizer vocabularies favor
    /// common words, so "hey lancia" often arrives as "hey lancer". Next to a
    /// clean "hey" every brand word may also slip Damerau-steps (one for short
    /// names, two for long); a garbled interjection ("pay alpine" is a literal
    /// transcript we logged) still demands the exact brand, so a double
    /// mishearing never wakes the overlay. <paramref name="confusions"/> is the
    /// same logged-mishearing table buttons resolve through, consulted as a
    /// second voice of the same utterance: the transcript is scanned again with
    /// the swaps applied, so "hair lamborghini" wakes once the table says hair
    /// means hey. The tolerances never move: the table can only add doors.
    /// One stop word doubles as an interjection after all: "the"/"they" —
    /// the words the logs caught where "hey" was said — wake on the brand's
    /// exact word run only. The free-running transcript floor is a wall of
    /// bare "the"s (2026-10-14: dozens between attempts), but the floor
    /// never carries the driven brand, so an exact brand name behind a
    /// clipped interjection is the player, not chatter. Aliases stay out of
    /// that door: "the lord of" is a phrase, not a wake. And when even the
    /// clipped stand-in dies whole — the logged 17:30:07 final "pasta" —
    /// a complete <paramref name="isFinal"/> utterance that is exactly one
    /// alias word wakes: the silence floor never solos an alias, partials
    /// keep growing, and the bare brand word stays chatter by the same rule.
    /// </summary>
    public static bool IsWake(string text, string brand, IReadOnlyList<string>? aliases = null,
        IReadOnlyDictionary<string, string>? confusions = null, bool isFinal = false)
    {
        string[] words = Split(text);
        string[] brandRun = Split(brand);
        List<string[]> targets = Targets(brand, aliases);

        if (WakeIn(words, brandRun, targets)) return true;
        if (confusions is { Count: > 0 }
            && WakeIn(Resolve(words, confusions), brandRun, targets)) return true;
        return isFinal && SoloAlias(words, aliases);
    }

    // One hearing of the transcript (raw words, or the confusion-resolved
    // second opinion) scanned for the wake pattern.
    private static bool WakeIn(string[] words, string[] brandRun, List<string[]> targets)
    {
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i] == Interjection)
            {
                if (TargetAt(words, i + 1, targets, tolerant: true)) return true;
            }
            else if (!CommonWords.Contains(words[i]) &&
                     Distance(words[i], Interjection) <= InterjectionSlack)
            {
                if (TargetAt(words, i + 1, targets, tolerant: false)) return true;
            }
            else if (ClippedHey.Contains(words[i]) &&
                     ExactRunAt(words, i + 1, brandRun))
            {
                return true;
            }
        }

        return false;
    }

    // Exact-only brand witness for the clipped-"hey" door: no Damerau slip,
    // no alias run — see IsWake.
    private static bool ExactRunAt(string[] words, int start, string[] brandRun) =>
        brandRun.Length > 0 && start + brandRun.Length <= words.Length &&
        words.AsSpan(start, brandRun.Length).SequenceEqual(brandRun);

    // Solo door: the 2026-10-14 17:30:07 miss — "hey mazda" landed as the
    // bare final "pasta", the interjection dead whole, leaving no word any
    // interjection door could stand on. A FINAL utterance that is exactly
    // one logged alias is then the player driving the brand through its
    // documented garble: the same log's silence floor is "the" and "hm"
    // (17:31-17:36, nobody speaking), never an alias word, so the table
    // alone decides. Partials keep the utterance still growing, and the
    // bare brand word keeps never waking — chatter can name the car.
    private static bool SoloAlias(string[] words, IReadOnlyList<string>? aliases)
    {
        if (words.Length != 1 || aliases is null)
        {
            return false;
        }

        foreach (string alias in aliases)
        {
            string[] run = Split(alias);
            if (run.Length == 1 && run[0] == words[0])
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the label's words answer the text as one whole
    /// word run, anywhere — the same door MatchLabel scores first. The
    /// screen controller uses it to spot one utterance's tail fragment after
    /// a navigation: Vosk's endpoint re-scores leading words ("we continue"
    /// became the final "the continue", logged 2026-10-14), but the command
    /// words themselves survive, and they are the tap's identity.</summary>
    public static bool AnswersText(string label, string text) =>
        ContainsRun(Split(text), Split(label));

    /// <summary>
    /// Best button label answered by the text: the whole label as consecutive
    /// words anywhere, or a leading slice of it (two words minimum — a lone
    /// "open" or "cancel" is too generic to fire anything). Whole-label answers
    /// always beat slice answers; among equals, longest wins and the first key
    /// wins ties. Null when nothing matches; the caller maps the label to its
    /// action, so Core stays free of UI delegates.
    /// <paramref name="confusions"/> maps words as the English model logs them
    /// to the single label word they swallow (keys already normalized by the
    /// loader). The garbled reading is scored as a second voice of the same
    /// utterance, never instead of it: a button that answers to the exact
    /// words keeps answering, so an alias can only add doors, not move one.
    /// </summary>
    public static string? MatchLabel(string text, IEnumerable<string> labels,
        IReadOnlyDictionary<string, string>? confusions = null)
    {
        string[] words = Split(text);
        string[]? resolved = confusions is { Count: > 0 } ? Resolve(words, confusions) : null;
        string? best = null;
        int bestScore = 0;
        foreach (string label in labels)
        {
            string[] parts = Split(label);
            int score = AnswerScore(words, parts);
            if (resolved is not null)
            {
                score = Math.Max(score, AnswerScore(resolved, parts));
            }
            if (score > bestScore)
            {
                best = label;
                bestScore = score;
            }
        }

        return best;
    }

    // Word-for-word substitution over the already-split transcript; a miss
    // keeps the heard word, so the resolved run is just a second opinion.
    private static string[] Resolve(string[] words, IReadOnlyDictionary<string, string> confusions)
    {
        string[] resolved = new string[words.Length];
        Array.Copy(words, resolved, words.Length);
        for (int i = 0; i < resolved.Length; i++)
        {
            if (confusions.TryGetValue(resolved[i], out string? intent) && intent.Length > 0)
            {
                resolved[i] = Normalize(intent);
            }
        }

        return resolved;
    }

    private static List<string[]> Targets(string brand, IReadOnlyList<string>? aliases)
    {
        List<string[]> targets = new() { Split(brand) };
        if (aliases is not null)
        {
            foreach (string alias in aliases)
            {
                targets.Add(Split(alias));
            }
        }

        targets.RemoveAll(t => t.Length == 0);
        return targets;
    }

    private static bool TargetAt(string[] words, int start, List<string[]> targets, bool tolerant)
    {
        foreach (string[] target in targets)
        {
            if (start + target.Length > words.Length)
            {
                continue;
            }

            if (RunsMatch(words.AsSpan(start, target.Length), target, tolerant))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RunsMatch(ReadOnlySpan<string> spoken, string[] target, bool tolerant)
    {
        for (int j = 0; j < target.Length; j++)
        {
            if (spoken[j] == target[j])
            {
                continue;
            }

            if (!tolerant || Distance(spoken[j], target[j]) > BrandSlack(target[j]))
            {
                return false;
            }
        }

        return true;
    }

    // One letter of garble is free, two only on long names where syllables
    // genuinely swallow each other ("lambogini"). Short names stay strict:
    // "lancer" must not wake "lancia" until that mishearing is documented
    // in the alias table.
    private static int BrandSlack(string brandWord) => brandWord.Length <= 6 ? 1 : 2;

    // Whole label as a word run: bonus-scored, so a keyword alias always
    // beats a different label's leading slice regardless of registration
    // order ("where is" for a garbled "Where is Willy"). Otherwise the
    // longest leading slice, two words minimum; single-word labels answer
    // whole or not at all.
    private static int AnswerScore(string[] words, string[] parts)
    {
        if (parts.Length == 0)
        {
            return 0;
        }

        if (ContainsRun(words, parts))
        {
            return WholeLabelBonus + parts.Length;
        }

        for (int k = parts.Length - 1; k >= 2; k--)
        {
            if (ContainsRun(words, parts.AsSpan(0, k)))
            {
                return k;
            }
        }

        return 0;
    }

    private static bool ContainsRun(string[] words, ReadOnlySpan<string> run)
    {
        for (int i = 0; i <= words.Length - run.Length; i++)
        {
            if (words.AsSpan(i, run.Length).SequenceEqual(run))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Split(string text) =>
        Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    // Damerau-Levenshtein: adjacent transpositions count as one slip ("auid"
    // is "audi" said fast). Callers compare against 1 or 2, so far jumps
    // bail out early on the length difference.
    private static int Distance(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 3)
        {
            return 4;
        }

        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++)
        {
            d[i, 0] = i;
        }

        for (int j = 0; j <= b.Length; j++)
        {
            d[0, j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
                }
            }
        }

        return d[a.Length, b.Length];
    }
}
