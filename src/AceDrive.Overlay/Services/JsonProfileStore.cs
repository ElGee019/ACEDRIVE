using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Player name + home address in %AppData%\AceDrive\profile.json. Never
/// throws: a missing or corrupt file means the opening screen starts from
/// the Steam name, and a failed write must not break gameplay or shutdown.
/// </summary>
public sealed class JsonProfileStore : IProfileStore
{
    private readonly string _file;
    private readonly ILog _log;
    private readonly object _gate = new();
    private PlayerProfile? _current;
    private bool _loaded;

    public JsonProfileStore(ILog log)
        : this(DataPaths.UserSave("profile.json"), log)
    { }

    public JsonProfileStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    // The online boards ask for the current display name on every refresh.
    // Cache the loaded profile (Save refreshes it) so that lookup never turns
    // into a file read per settled job.
    public PlayerProfile? Load()
    {
        lock (_gate)
        {
            if (_loaded) return _current;
            _loaded = true;
            _current = Read();
            return _current;
        }
    }

    public void Save(PlayerProfile profile)
    {
        if (!profile.IsComplete)
        {
            _log.LogWarning("profile not saved: incomplete address");
            return;
        }

        // A failed write must never break gameplay or shutdown; the opening
        // screen already holds the values in memory.
        if (!JsonFile.TryWrite(_file, profile, out string? error))
        {
            _log.LogWarning(
                $"profile.json unwritable at {_file} ({error}); profile kept in memory");
        }

        lock (_gate)
        {
            _current = profile;
            _loaded = true;
        }
    }

    private PlayerProfile? Read()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out PlayerProfile? profile, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return null;
        }

        // A corrupt or locked save degrades to a blank opening screen, never
        // takes the overlay down.
        if (profile is null || !profile.IsComplete)
        {
            _log.LogWarning(
                $"profile.json {JsonFile.Describe(error)} at {_file}; " +
                "opening screen starts from Steam");
            return null;
        }

        _log.LogInfo($"profile loaded: {profile.DisplayName}, " +
            $"{profile.Country} {profile.City} {profile.StreetId} {profile.HouseNumber}");
        return profile;
    }
}
