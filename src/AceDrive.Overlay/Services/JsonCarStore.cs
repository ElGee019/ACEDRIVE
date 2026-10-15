using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads the road-test pool from Data/cars.json next to the exe. Missing or
/// invalid file degrades to an empty pool (the Tuesday row then shows the
/// plain "Road test" label) — the overlay must never fail to start over a
/// data file.
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

    public IReadOnlyList<RoadTestCar> Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                _log.LogWarning($"cars.json missing at {_file}; road test runs without a pool");
                return [];
            }
            List<RoadTestCar>? cars = JsonSerializer.Deserialize<List<RoadTestCar>>(
                File.ReadAllText(_file), JsonOptions);
            if (cars is null || cars.Count == 0)
            {
                _log.LogWarning($"cars.json invalid at {_file}; road test runs without a pool");
                return [];
            }
            List<RoadTestCar> valid = [];
            foreach (RoadTestCar car in cars)
            {
                if (car.Id.Length > 0 && car.Year > 1900 && car.Brand.Length > 0 && car.Model.Length > 0)
                {
                    valid.Add(car);
                }
            }
            if (valid.Count == 0)
            {
                _log.LogWarning($"cars.json has no usable rows at {_file}; road test runs without a pool");
                return [];
            }
            if (valid.Count != cars.Count)
            {
                _log.LogWarning($"cars.json dropped {cars.Count - valid.Count} invalid rows at {_file}");
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
}
