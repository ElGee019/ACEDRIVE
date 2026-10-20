using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the road-test pool from Data/cars.json next to the exe. Missing or
/// invalid file degrades to an empty pool (the Tuesday row then shows the
/// plain "Road test" label) — the overlay must never fail to start over a
/// data file. Rows may carry a mechanical preset (showroom variant,
/// PORTPLAN 62); an empty preset normalizes to null and rows sharing one
/// ledger key drop so the bank can never double-count a car.
/// </summary>
public sealed class JsonCarStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonCarStore(ILog log)
        : this(Path.Combine(AppContext.BaseDirectory, "Data", "cars.json"), log)
    { }

    public JsonCarStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<CarSpec> Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"cars.json missing at {_file}; road test runs without a pool");
                return [];
            }
            List<CarSpec>? cars = JsonSerializer.Deserialize<List<CarSpec>>(
                File.ReadAllText(_file), JsonOptions);
            if (cars is null || cars.Count == 0)
            {
                _log.LogWarning($"cars.json invalid at {_file}; road test runs without a pool");
                return [];
            }
            List<CarSpec> valid = [];
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (CarSpec car in cars)
            {
                CarSpec row = car.Preset is { Length: 0 } ? car with { Preset = null } : car;
                if (IsUsable(row) && keys.Add(row.Key))
                {
                    valid.Add(row);
                }
            }
            if (valid.Count == 0)
            {
                _log.LogWarning($"cars.json has no usable rows at {_file}; road test runs without a pool");
                return [];
            }
            if (valid.Count != cars.Count)
            {
                _log.LogWarning($"cars.json dropped {cars.Count - valid.Count} unusable rows at {_file}");
            }
            _log.LogInfo($"cars loaded: {valid.Count} road-test candidates");
            return valid;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked data file
            // must not take the overlay down (AGENTS.md: Try* at every
            // external boundary). Anything else stays a crash.
            _log.LogWarning($"cars.json unreadable at {_file} ({ex.GetType().Name}); road test runs without a pool");
            return [];
        }
    }

    // A row is usable when it can be displayed, road-tested and sold: identity
    // present, year plausible, and no negative spec or money value. A negative
    // price in particular would pay the player on a purchase (the dealer keys
    // its rule on Price), so a corrupt row drops out here instead of reaching
    // the dealer, the garage or the receipt.
    private static bool IsUsable(CarSpec car) =>
        car.Id.Length > 0 && car.Year > 1900 && car.Brand.Length > 0 && car.Model.Length > 0
        && car.Price >= 0.0
        && car.TrunkLitres >= 0
        && car.Horsepower >= 0
        && car.EngineCc >= 0
        && car.MassKg >= 0
        && car.TopSpeedKmh >= 0
        && car.Acceleration >= 0.0
        && car.Gears >= 0
        && car.TankLitres >= 0.0
        && car.FuelPer100Km >= 0.0
        && car.BrakeDistance >= 0.0;
}
