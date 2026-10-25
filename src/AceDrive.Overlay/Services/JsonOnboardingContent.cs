using System.IO;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads onboarding content (brands, aliases, cars, dialogue) from a Data
/// directory. Missing or invalid files degrade to empty lists with a warning —
/// the overlay must never fail to start over a data file. The Content view
/// re-reads when the directory's newest write time moves, so editing the
/// voice tables against the running overlay takes effect on the next
/// utterance instead of silently testing a stale snapshot (2026-10-14
/// wake-word sessions: minutes of "hey mazda" retried against a process that
/// had loaded the aliases before they existed).
/// </summary>
public sealed class JsonOnboardingContent : IContentSource
{
    private readonly string _dir;
    private readonly ILog _log;
    private OnboardingContent? _snapshot;
    private DateTime _stamp;

    public JsonOnboardingContent(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data"), log)
    { }

    public JsonOnboardingContent(string dir, ILog log)
    {
        _dir = dir;
        _log = log;
    }

    /// <summary>The loaded bundle, re-read whenever the Data files change.</summary>
    public OnboardingContent Content
    {
        get
        {
            DateTime stamp = NewestWrite();
            if (_snapshot is null || stamp != _stamp)
            {
                // WHY stamp before Load: a file rewritten mid-read makes the
                // next probe differ from this stamp and re-read, instead of
                // pinning the half-written table until the following edit.
                _stamp = stamp;
                _snapshot = Load();
            }

            return _snapshot;
        }
    }

    // Best-effort staleness probe: newest *.json write in the data dir, plus
    // the file count. One scan per voice utterance at most — never in the
    // 10 Hz poll.
    private DateTime NewestWrite()
    {
        try
        {
            DateTime newest = default;
            int count = 0;
            foreach (string file in Directory.EnumerateFiles(_dir, "*.json"))
            {
                count++;
                DateTime write = File.GetLastWriteTimeUtc(file);
                if (write > newest) newest = write;
            }

            // WHY the count rides along in a millisecond: a build that copies
            // in a whole new Data set changes the count without moving the
            // max stamp past an older one; the probe must notice both.
            return newest.AddMilliseconds(count);
        }
        // WHY: a transient lock on the data dir must keep answering from the
        // last snapshot; a failed probe may never fail or mute the voice path.
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return _stamp;
        }
    }

    public OnboardingContent Load()
    {
        string[] brands = Read("brands.json", Array.Empty<string>());
        Dictionary<string, string[]> aliases =
            Read("brand_aliases.json", new Dictionary<string, string[]>());
        List<RawCar> cars = Read("grandma_cars.json", new List<RawCar>());
        List<RawLine> dialogue = Read("grandma_dialogue.json", new List<RawLine>());
        List<RawLine> pancakes = Read("pancake_dialogue.json", new List<RawLine>());
        List<RawLine> communityCentre =
            Read("community_centre_dialogue.json", new List<RawLine>());
        List<RawLine> maxDialogue = Read("max_dialogue.json", new List<RawLine>());
        Dictionary<string, string> confusions = CleanConfusions(
            Read("voice_confusions.json", new Dictionary<string, string>()));

        Array.Sort(brands, StringComparer.Ordinal);
        var content = new OnboardingContent(
            brands.Where(b => !string.IsNullOrWhiteSpace(b)).ToArray(),
            aliases.Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value is not null)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            cars.Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new GrandmaCar(c.Id, c.Name, c.Year, c.Price, ThumbFor(c.Id)))
                .ToArray(),
            dialogue.Where(l => !string.IsNullOrWhiteSpace(l.Text))
                .Select(l => new DialogueLine(l.Speaker ?? "", l.Text!, l.Note))
                .ToArray(),
            pancakes.Where(l => !string.IsNullOrWhiteSpace(l.Text))
                .Select(l => new DialogueLine(l.Speaker ?? "", l.Text!, l.Note))
                .ToArray(),
            communityCentre.Where(l => !string.IsNullOrWhiteSpace(l.Text))
                .Select(l => new DialogueLine(l.Speaker ?? "", l.Text!, l.Note))
                .ToArray(),
            maxDialogue.Where(l => !string.IsNullOrWhiteSpace(l.Text))
                .Select(l => new DialogueLine(l.Speaker ?? "", l.Text!, l.Note))
                .ToArray(),
            confusions);
        _log.LogInfo($"content: {content.Brands.Count} brands, {content.Cars.Count} cars " +
            $"({content.Cars.Count(c => c.ImagePath is not null)} with thumbs), " +
            $"{content.Dialogue.Count} dialogue lines, " +
            $"{content.PancakeDialogue.Count} pancake lines, " +
            $"{content.CommunityCentreDialogue.Count} community centre lines, " +
            $"{content.MaxDialogue.Count} Max delivery-intro lines, " +
            $"{confusions.Count} transcript aliases");
        return content;
    }

    // Transcript aliases are single normalized word pairs: the matching in
    // VoiceMatch swaps one heard word for one label word, so entries with
    // empty or multi-word halves cannot be honored and are dropped here
    // rather than silently failing at match time.
    private static Dictionary<string, string> CleanConfusions(Dictionary<string, string> raw)
    {
        var clean = new Dictionary<string, string>();
        foreach (KeyValuePair<string, string> kv in raw)
        {
            string heard = VoiceMatch.Normalize(kv.Key);
            string intent = VoiceMatch.Normalize(kv.Value);
            if (heard.Length == 0 || intent.Length == 0
                || heard.Contains(' ') || intent.Contains(' ') || heard == intent)
            {
                continue;
            }
            clean[heard] = intent;
        }
        return clean;
    }

    // Shipped studio renders are repaints of the game's own visual_1 shots
    // (MX-5/Datsun piano black, Abarth dark navy): grandma's offer cars are
    // game content, so we show one canonical paint, not the player's garage.
    private string? ThumbFor(string id)
    {
        string png = Path.Combine(_dir, "Thumbs", id + ".png");
        return File.Exists(png) ? png : null;
    }

    // One section of the bundle; a missing or corrupt file leaves that section
    // empty and the rest of the onboarding still loads.
    private T Read<T>(string name, T fallback)
        where T : class
    {
        string file = Path.Combine(_dir, name);
        JsonReadStatus status = JsonFile.TryRead(file, out T? value, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            _log.LogWarning($"{name} missing in {_dir}; onboarding section empty");
            return fallback;
        }

        // A corrupt or locked data file must not take the overlay down
        // (AGENTS.md: Try* at every external boundary).
        if (value is null)
        {
            _log.LogWarning(
                $"{name} unreadable in {_dir} ({error}); onboarding section empty");
            return fallback;
        }

        return value;
    }

    // Deserialization DTOs: tolerant (missing fields default), validated on map.
    private sealed class RawCar
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int Year { get; set; }
        public int Price { get; set; }
    }

    private sealed class RawLine
    {
        public string? Speaker { get; set; }
        public string? Text { get; set; }

        // Aside printed under the spoken page, never spoken by the character.
        public string? Note { get; set; }
    }
}
