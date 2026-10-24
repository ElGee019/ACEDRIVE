using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The addresses page's directory in %AppData%\AceDrive\addresses.json. Missing,
/// corrupt or hand-edited files degrade to an empty list: the boot seeder then
/// draws the at-start businesses again instead of blocking startup.
/// </summary>
public sealed class JsonPoiDirectoryStore : IPoiDirectoryStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonPoiDirectoryStore(ILog log)
        : this(DataPaths.UserSave("addresses.json"), log)
    { }

    public JsonPoiDirectoryStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public IReadOnlyList<PointOfInterest> Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out PointOfInterest[]? pois, out string? error);
        if (status == JsonReadStatus.Unreadable)
        {
            // A corrupt or locked file must not take the overlay down; the
            // boot seeder rebuilds the at-start doors.
            _log.LogWarning($"addresses.json unreadable at {_file} ({error})");
        }

        return Valid(pois);
    }

    public void Save(IReadOnlyList<PointOfInterest> pois)
    {
        if (pois is null)
        {
            return;
        }

        // Discovery is flavour, not paid progress: a locked save must not
        // break the session that just found a door.
        if (!JsonFile.TryWrite(_file, pois, out string? error))
        {
            _log.LogWarning($"addresses.json unwritable at {_file} ({error})");
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
