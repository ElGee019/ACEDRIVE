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
    private readonly string _file;
    private readonly ILog _log;
    private readonly Dictionary<string, double> _km;

    public JsonBrandMileageStore(ILog log)
        : this(DataPaths.UserSave("brand_mileage.json"), log)
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
        // A failed bank must never break gameplay or shutdown; the next ride
        // retries with fresher numbers.
        if (!JsonFile.TryWrite(_file, _km, out string? error))
        {
            _log.LogWarning(
                $"brand_mileage.json unwritable at {_file} ({error}); progress kept in memory");
        }
    }

    private static Dictionary<string, double> LoadKm(string file, ILog log)
    {
        var km = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        JsonReadStatus status = JsonFile.TryRead(
            file, out Dictionary<string, double>? raw, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return km;
        }

        // A corrupt save degrades to zero mileage everywhere, never takes the
        // overlay down.
        if (raw is null)
        {
            log.LogWarning(
                $"brand_mileage.json unreadable at {file} ({error}); starting at zero mileage");
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

    private static string Normalize(string? brand) => (brand ?? "").Trim();
}
