using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the placeholder free-roam address map from Data/address_map.json
/// next to the exe. Missing or invalid files degrade to the built-in Brands
/// Hatch fallback with a warning — the overlay must never fail to start over
/// a data file.
/// </summary>
public sealed class JsonAddressMapStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonAddressMapStore(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "address_map.json"), log)
    { }

    public JsonAddressMapStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public AddressMap Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"address_map.json missing at {_file}; using built-in Brands Hatch map");
                return AddressMap.Default;
            }

            AddressMap? map = JsonSerializer.Deserialize<AddressMap>(
                File.ReadAllText(_file), JsonOptions);
            if (map is null || !IsValid(map))
            {
                _log.LogWarning($"address_map.json invalid at {_file}; using built-in Brands Hatch map");
                return AddressMap.Default;
            }

            _log.LogInfo($"address map loaded: {map.Country}, {map.City}, " +
                $"{map.TrackLengthMeters:0} m, {map.Streets.Count} streets");
            return map;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"address_map.json unreadable at {_file} ({ex.GetType().Name}); using built-in Brands Hatch map");
            return AddressMap.Default;
        }
    }

    private static bool IsValid(AddressMap map)
    {
        if (string.IsNullOrWhiteSpace(map.Country)
            || string.IsNullOrWhiteSpace(map.City)
            || !double.IsFinite(map.TrackLengthMeters)
            || map.TrackLengthMeters <= 0.0
            || !double.IsFinite(map.HouseSpacingMeters)
            || map.HouseSpacingMeters <= 0.0
            || map.Streets is null
            || map.Streets.Count == 0)
        {
            return false;
        }

        foreach (AddressStreet street in map.Streets)
        {
            if (street is null
                || string.IsNullOrWhiteSpace(street.Id)
                || string.IsNullOrWhiteSpace(street.Name)
                || !double.IsFinite(street.FromFraction)
                || !double.IsFinite(street.ToFraction)
                || street.FromFraction < 0.0
                || street.ToFraction > 1.0
                || street.ToFraction <= street.FromFraction)
            {
                return false;
            }
        }

        return true;
    }
}
