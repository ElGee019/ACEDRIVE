using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Player name + home address in %AppData%\AceDrive\profile.json. Never
/// throws: a missing or corrupt file means the opening screen starts from
/// the Steam name, and a failed write must not break gameplay or shutdown.
/// </summary>
public sealed class JsonProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonProfileStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "profile.json"), log)
    { }

    public JsonProfileStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public PlayerProfile? Load()
    {
        try
        {
            if (!File.Exists(_file)) return null;
            PlayerProfile? profile = JsonSerializer.Deserialize<PlayerProfile>(
                File.ReadAllText(_file), JsonOptions);
            if (profile is null || !profile.IsComplete)
            {
                _log.LogWarning($"profile.json incomplete at {_file}; opening screen starts from Steam");
                return null;
            }

            _log.LogInfo($"profile loaded: {profile.DisplayName}, " +
                $"{profile.Country} {profile.City} {profile.StreetId} {profile.HouseNumber}");
            return profile;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt or locked save must
            // degrade to a blank opening screen, never take the overlay down.
            _log.LogWarning($"profile.json unreadable at {_file} ({ex.GetType().Name}); opening screen starts from Steam");
            return null;
        }
    }

    public void Save(PlayerProfile profile)
    {
        if (!profile.IsComplete)
        {
            _log.LogWarning("profile not saved: incomplete address");
            return;
        }

        try
        {
            string? dir = Path.GetDirectoryName(_file);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_file, JsonSerializer.Serialize(profile, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed write must never break gameplay or shutdown; the
            // opening screen already holds the values in memory.
            _log.LogWarning($"profile.json unwritable at {_file} ({ex.GetType().Name}); profile kept in memory");
        }
    }
}
