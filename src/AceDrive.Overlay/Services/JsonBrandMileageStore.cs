using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves brand mileage under %AppData%\AceDrive. Never throws: a
/// missing or corrupt file means every brand starts at 0 km, and a failed
/// write must never break gameplay or shutdown. Keys are case-insensitive
/// (the watcher publishes lowercase spoken brands, onboarding Title Case).
/// </summary>
public sealed class JsonBrandMileageStore : IBrandMileageStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;
    private readonly Dictionary<string, double> _km;

    public JsonBrandMileageStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "brand_mileage.json"), log)
    { }

    public JsonBrandMileageStore(string file, ILog log)
    {
        _file = file;
        _log = log;
        _km = LoadKm(file, log);
    }

    public double GetKm(string brand)
    {
        string key = Normalize(brand);
        return key.Length == 0 ? 0.0 : Math.Max(0.0, _km.GetValueOrDefault(key));
    }

    public double RegisterRide(string brand, double distanceKm)
    {
        string raw = (brand ?? "").Trim();
        string key = Normalize(raw);
        if (key.Length == 0)
        {
            return 0.0;
        }
        double total = Math.Max(0.0, _km.GetValueOrDefault(key)) + Math.Max(0.0, distanceKm);
        _km[key] = total;
        Save();
        return total;
    }

    private void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(_file);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_file, JsonSerializer.Serialize(_km, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed bank must never break gameplay or shutdown; the next
            // ride retries with fresher numbers.
            _log.LogWarning($"brand_mileage.json unwritable at {_file} ({ex.GetType().Name}); progress kept in memory");
        }
    }

    private static Dictionary<string, double> LoadKm(string file, ILog log)
    {
        try
        {
            if (!File.Exists(file))
            {
                return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            }
            var raw = JsonSerializer.Deserialize<Dictionary<string, double>>(
                File.ReadAllText(file), JsonOptions);
            var km = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            if (raw is null)
            {
                return km;
            }
            foreach (var (brand, distance) in raw)
            {
                string key = (brand ?? "").Trim();
                if (key.Length > 0 && distance >= 0.0 && !double.IsNaN(distance) && !double.IsInfinity(distance))
                {
                    km[key] = distance;
                }
            }
            return km;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt save degrades to
            // zero mileage everywhere, never takes the overlay down.
            log.LogWarning($"brand_mileage.json unreadable at {file} ({ex.GetType().Name}); starting at zero mileage");
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string Normalize(string? brand) => (brand ?? "").Trim();
}
