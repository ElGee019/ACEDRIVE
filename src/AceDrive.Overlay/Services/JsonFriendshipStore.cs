using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves familiarity under %AppData%\AceDrive. Never throws: a
/// missing or corrupt file means every resident starts at 0, and a failed
/// write must never break gameplay or shutdown. Keys are case-insensitive
/// (Unity matches ownerName OrdinalIgnoreCase).
/// </summary>
public sealed class JsonFriendshipStore : IFriendshipStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;
    private readonly Dictionary<string, int> _levels;

    public JsonFriendshipStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "friendship.json"), log)
    { }

    public JsonFriendshipStore(string file, ILog log)
    {
        _file = file;
        _log = log;
        _levels = LoadLevels(file, log);
    }

    public int GetLevel(string ownerName)
    {
        string key = Normalize(ownerName);
        return key.Length == 0 ? 0 : _levels.GetValueOrDefault(key);
    }

    public FriendshipOutcome RegisterRide(string ownerName) =>
        Apply(ownerName, 1);

    public void AddFamiliarity(string ownerName, int delta) =>
        _ = Apply(ownerName, delta);

    private FriendshipOutcome Apply(string ownerName, int delta)
    {
        string raw = (ownerName ?? "").Trim();
        string key = Normalize(raw);
        if (key.Length == 0)
        {
            return new FriendshipOutcome("", 0, false);
        }
        int before = _levels.GetValueOrDefault(key);
        int after = Math.Max(0, before + delta);
        _levels[key] = after;
        Save();
        return new FriendshipOutcome(raw, after, FriendshipLevels.TierIncreased(before, after));
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
            File.WriteAllText(_file, JsonSerializer.Serialize(_levels, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed bank must never break gameplay or shutdown; the next
            // ride retries with fresher numbers.
            _log.LogWarning($"friendship.json unwritable at {_file} ({ex.GetType().Name}); progress kept in memory");
        }
    }

    private static Dictionary<string, int> LoadLevels(string file, ILog log)
    {
        try
        {
            if (!File.Exists(file))
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
            var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(
                File.ReadAllText(file), JsonOptions);
            var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (raw is null)
            {
                return levels;
            }
            foreach (var (name, level) in raw)
            {
                string key = (name ?? "").Trim();
                if (key.Length > 0 && level >= 0)
                {
                    levels[key] = level;
                }
            }
            return levels;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Read/deserialize boundary only: a corrupt save degrades to
            // strangers everywhere, never takes the overlay down.
            log.LogWarning($"friendship.json unreadable at {file} ({ex.GetType().Name}); starting strangers");
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string Normalize(string? ownerName) => (ownerName ?? "").Trim();
}
