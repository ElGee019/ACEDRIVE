using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the hub career table from Data/delivery_hubs.json next to the exe.
/// Missing or invalid files degrade to <see cref="HubCatalog.Default"/> (no
/// career can be opened) with a warning — the overlay must never fail to
/// start over a data file, and the hub page shows its COMING SOON state.
/// </summary>
public sealed class JsonHubCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonHubCatalog(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "delivery_hubs.json"), log)
    { }

    public JsonHubCatalog(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public HubCatalog Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"delivery_hubs.json missing at {_file}; using built-in defaults");
                return HubCatalog.Default;
            }
            var catalog = JsonSerializer.Deserialize<HubCatalog>(
                File.ReadAllText(_file), JsonOptions);
            if (catalog is null || !IsValid(catalog))
            {
                _log.LogWarning($"delivery_hubs.json invalid at {_file}; using built-in defaults");
                return HubCatalog.Default;
            }
            _log.LogInfo($"hub catalog loaded: {catalog.Tiers.Count} tiers");
            return catalog;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"delivery_hubs.json unreadable at {_file} ({ex.GetType().Name}); using built-in defaults");
            return HubCatalog.Default;
        }
    }

    // Unity tracks hubs by name across the whole catalog, so names must be
    // globally unique or ownership checks (and the CredP ramp keys) collide.
    // Enabled tiers need at least one career; every career needs an id to
    // gate stores on, a label to show and priced, named levels. The ramp
    // arrays must be parallel (one rate + cap per level), and owner names
    // must be non-empty and unique like the hub names they label.
    private static bool IsValid(HubCatalog catalog)
    {
        if (catalog.Ramp is { } ramp
            && (ramp.StartRates.Count == 0
                || ramp.StartRates.Count != ramp.Caps.Count
                || ramp.StartRates.Any(r => r <= 0)
                || ramp.Caps.Any(c => c <= 0)))
        {
            return false;
        }
        var ownerNames = new HashSet<string>();
        foreach (string owner in catalog.OwnerNames ?? [])
        {
            if (owner.Length == 0 || !ownerNames.Add(owner)) return false;
        }
        var tierIds = new HashSet<int>();
        var hubNames = new HashSet<string>();
        foreach (HubTier tier in catalog.Tiers)
        {
            if (tier.Id <= 0 || !tierIds.Add(tier.Id)) return false;
            if (tier.Enabled && tier.Careers.Count == 0) return false;
            foreach (HubCareer career in tier.Careers)
            {
                if (career.Id.Length == 0 || career.Label.Length == 0
                    || career.Levels.Count == 0)
                {
                    return false;
                }
                foreach (HubLevel level in career.Levels)
                {
                    if (level.Name.Length == 0 || level.Cost <= 0 || !hubNames.Add(level.Name))
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }
}
