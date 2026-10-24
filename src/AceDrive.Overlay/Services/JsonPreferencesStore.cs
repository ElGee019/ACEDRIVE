using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Distance unit + UI language in %AppData%\AceDrive\preferences.json. Never
/// throws: a missing or corrupt file means the defaults, and a failed write
/// must not break gameplay or shutdown.
/// </summary>
public sealed class JsonPreferencesStore : IPreferencesStore
{
    private readonly string _file;
    private readonly ILog _log;

    public JsonPreferencesStore(ILog log)
        : this(DataPaths.UserSave("preferences.json"), log)
    { }

    public JsonPreferencesStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public PlayerPreferences Load()
    {
        JsonReadStatus status = JsonFile.TryRead(_file, out PlayerPreferences? preferences, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return PlayerPreferences.Default;
        }

        // WHY the enum guard: an unknown unit/language in a hand-edited file
        // must fall back, not render an undefined option.
        if (preferences is null
            || !Enum.IsDefined(preferences.Units)
            || !Enum.IsDefined(preferences.Language))
        {
            _log.LogWarning(
                $"preferences.json {JsonFile.Describe(error)} at {_file}; using the defaults");
            return PlayerPreferences.Default;
        }

        return preferences;
    }

    public void Save(PlayerPreferences preferences)
    {
        // A failed write must never break gameplay or shutdown; the
        // preferences screen already holds the values in memory.
        if (!JsonFile.TryWrite(_file, preferences, out string? error))
        {
            _log.LogWarning($"preferences.json unwritable at {_file} ({error}); kept in memory");
        }
    }
}
