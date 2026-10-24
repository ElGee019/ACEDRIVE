using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the POI name table from Data/pois.json next to the exe. Missing,
/// empty or invalid files degrade to the built-in Max's Grocery Store and
/// business fallback with a warning — a content file must never block the
/// opening dialogue.
/// </summary>
public sealed class JsonPoiStore
{
    // The one table whose enums are authored as names (spawn pacing:
    // "streets", "either", …), never a bare ordinal a hand edit could shift,
    // so it cannot share the default options; the boundary still comes from
    // JsonFile.
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonPoiStore(ILog log)
        : this(DataPaths.GameData("pois.json"), log)
    { }

    public JsonPoiStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<PoiDefinition> Load()
    {
        if (!JsonTable.TryLoad(
                _log, _file, "pois.json", "using built-in POI table",
                IsValid, out PoiDefinition[]? pois, JsonOptions))
        {
            return PoiCatalog.Default;
        }

        _log.LogInfo($"POI table loaded: {pois.Length} point(s) of interest");
        return pois;
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
                || !ids.Add(poi.Id)
                || !IsUsableSpawn(poi))
            {
                return false;
            }
        }

        return true;
    }

    // A generated business needs a name pattern and a rule the spawner can
    // read; a fixed row carries neither. Rejecting the whole table keeps a
    // half-edited rule from opening a business with no name or pacing.
    private static bool IsUsableSpawn(PoiDefinition poi)
    {
        if (poi.Spawn is not { } rule)
        {
            return string.IsNullOrWhiteSpace(poi.NamePattern);
        }

        if (string.IsNullOrWhiteSpace(poi.NamePattern)
            || rule.Pacing == PoiPacing.None
            || rule.IntervalStreets <= 0)
        {
            return false;
        }

        return rule.Pacing switch
        {
            PoiPacing.Streets => rule.FirstStreets > 0,
            PoiPacing.JobsFirstThenStreets => rule.FirstJobs > 0 && rule.FirstStreets >= 0,
            PoiPacing.Either => rule.FirstJobs > 0 && rule.FirstStreets > 0,
            PoiPacing.OwnedCategory => !string.IsNullOrWhiteSpace(poi.Category),
            _ => false,
        };
    }
}
