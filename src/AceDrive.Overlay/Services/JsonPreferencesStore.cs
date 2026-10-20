using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Distance unit + UI language in %AppData%\AceDrive\preferences.json. Never
/// throws: a missing or corrupt file means the defaults, and a failed write
/// must not break gameplay or shutdown.
/// </summary>
public sealed class JsonPreferencesStore : IPreferencesStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonPreferencesStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "preferences.json"), log)
    { }

    public JsonPreferencesStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public PlayerPreferences Load()
    {
        try
        {
            if (!File.Exists(_file)) return PlayerPreferences.Default;
            PlayerPreferences? preferences = JsonSerializer.Deserialize<PlayerPreferences>(
                File.ReadAllText(_file), JsonOptions);
            // WHY the enum guard: an unknown unit/language in a hand-edited
            // file must fall back, not render an undefined option.
            if (preferences is null
                || !Enum.IsDefined(preferences.Units)
                || !Enum.IsDefined(preferences.Language))
            {
                _log.LogWarning($"preferences.json invalid at {_file}; using the defaults");
                return PlayerPreferences.Default;
            }

            return preferences;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked save must
            // degrade to the defaults, never take the overlay down.
            _log.LogWarning($"preferences.json unreadable at {_file} ({ex.GetType().Name}); using the defaults");
            return PlayerPreferences.Default;
        }
    }

    public void Save(PlayerPreferences preferences)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_file);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_file, JsonSerializer.Serialize(preferences, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed write must never break gameplay or shutdown; the
            // preferences screen already holds the values in memory.
            _log.LogWarning($"preferences.json unwritable at {_file} ({ex.GetType().Name}); kept in memory");
        }
    }
}
