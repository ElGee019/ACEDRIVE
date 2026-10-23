using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The addresses page's directory in %AppData%\AceDrive\addresses.json. Missing,
/// corrupt or hand-edited files degrade to an empty list: the boot seeder then
/// draws the at-start businesses again instead of blocking startup.
/// </summary>
public sealed class JsonPoiDirectoryStore : IPoiDirectoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonPoiDirectoryStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "addresses.json"), log)
    { }

    public JsonPoiDirectoryStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<PointOfInterest> Load()
    {
        try
        {
            if (!File.Exists(_file))
            {
                return [];
            }

            PointOfInterest[]? pois = JsonSerializer.Deserialize<PointOfInterest[]>(
                File.ReadAllText(_file), JsonOptions);
            return Valid(pois);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked file must
            // not take the overlay down (AGENTS: Try* at every external
            // boundary). The boot seeder rebuilds the at-start doors.
            _log.LogWarning($"addresses.json unreadable at {_file} ({ex.GetType().Name})");
            return [];
        }
    }

    public void Save(IReadOnlyList<PointOfInterest> pois)
    {
        if (pois is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(pois, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Discovery is flavour, not paid progress: a locked save must not
            // break the session that just found a door.
            _log.LogWarning($"addresses.json unwritable at {_file} ({ex.GetType().Name})");
        }
    }

    private static IReadOnlyList<PointOfInterest> Valid(PointOfInterest[]? pois)
    {
        var valid = new PoiDirectory();
        if (pois is null)
        {
            return valid.All;
        }

        // The directory owns the "usable door" rule, so a hand-edited file
        // can never put a row on the page that a fresh draw would reject.
        foreach (PointOfInterest poi in pois)
        {
            valid.Add(poi);
        }

        return valid.All;
    }
}
