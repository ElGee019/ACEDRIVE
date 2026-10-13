using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads onboarding content (brands, aliases, cars, dialogue) from a Data
/// directory. Missing or invalid files degrade to empty lists with a warning —
/// the overlay must never fail to start over a data file.
/// </summary>
public sealed class JsonOnboardingContent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _dir;
    private readonly ILog _log;

    public JsonOnboardingContent(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data"), log)
    { }

    public JsonOnboardingContent(string dir, ILog log)
    {
        _dir = dir;
        _log = log;
    }

    public OnboardingContent Load()
    {
        var brands = Read("brands.json", () => Array.Empty<string>()) ?? [];
        var aliases = Read("brand_aliases.json", () => new Dictionary<string, string[]>()) ?? new();
        var cars = Read("grandma_cars.json", () => new List<RawCar>()) ?? new();
        var dialogue = Read("grandma_dialogue.json", () => new List<RawLine>()) ?? new();

        Array.Sort(brands, StringComparer.Ordinal);
        var content = new OnboardingContent(
            brands.Where(b => !string.IsNullOrWhiteSpace(b)).ToArray(),
            aliases.Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value is not null)
                .ToDictionary(kv => kv.Key, kv => kv.Value),
            cars.Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new GrandmaCar(c.Id, c.Name, c.Year, c.Price, ThumbFor(c.Id)))
                .ToArray(),
            dialogue.Where(l => !string.IsNullOrWhiteSpace(l.Text))
                .Select(l => new DialogueLine(l.Speaker ?? "", l.Text!))
                .ToArray());
        _log.LogInfo($"content: {content.Brands.Count} brands, {content.Cars.Count} cars " +
            $"({content.Cars.Count(c => c.ImagePath is not null)} with thumbs), " +
            $"{content.Dialogue.Count} dialogue lines");
        return content;
    }

    // Shipped studio renders are repaints of the game's own visual_1 shots
    // (MX-5/Datsun piano black, Abarth dark navy): grandma's offer cars are
    // game content, so we show one canonical paint, not the player's garage.
    private string? ThumbFor(string id)
    {
        string png = Path.Combine(_dir, "Thumbs", id + ".png");
        return File.Exists(png) ? png : null;
    }

    private T? Read<T>(string name, Func<T> fallback)
    {
        string file = Path.Combine(_dir, name);
        try
        {
            if (!File.Exists(file))
            {
                _log.LogWarning($"{name} missing in {_dir}; onboarding section empty");
                return fallback();
            }
            return JsonSerializer.Deserialize<T>(File.ReadAllText(file), JsonOptions);
        }
        catch (Exception ex)
        {
            _log.LogWarning($"{name} unreadable in {_dir} ({ex.GetType().Name}); onboarding section empty");
            return fallback();
        }
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
    }
}
