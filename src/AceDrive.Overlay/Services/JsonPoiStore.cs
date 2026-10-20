using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the POI name table from Data/pois.json next to the exe. Missing,
/// empty or invalid files degrade to the built-in Max's Grocery Store
/// fallback with a warning — a content file must never block the opening
/// dialogue.
/// </summary>
public sealed class JsonPoiStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonPoiStore(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "pois.json"), log)
    { }

    public JsonPoiStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<PoiDefinition> Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"pois.json missing at {_file}; using built-in POI table");
                return PoiCatalog.Default;
            }

            PoiDefinition[]? pois = JsonSerializer.Deserialize<PoiDefinition[]>(
                File.ReadAllText(_file), JsonOptions);
            if (pois is null || !IsValid(pois))
            {
                _log.LogWarning($"pois.json invalid at {_file}; using built-in POI table");
                return PoiCatalog.Default;
            }

            _log.LogInfo($"POI table loaded: {pois.Length} point(s) of interest");
            return pois;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS: Try* at every external
            // boundary). Anything else stays a crash.
            _log.LogWarning($"pois.json unreadable at {_file} ({ex.GetType().Name}); using built-in POI table");
            return PoiCatalog.Default;
        }
    }

    private static bool IsValid(PoiDefinition[] pois)
    {
        if (pois.Length == 0)
        {
            return false;
        }

        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        foreach (PoiDefinition poi in pois)
        {
            if (poi is null
                || string.IsNullOrWhiteSpace(poi.Id)
                || string.IsNullOrWhiteSpace(poi.Name)
                || string.IsNullOrWhiteSpace(poi.Type)
                || !ids.Add(poi.Id))
            {
                return false;
            }
        }

        return true;
    }
}
