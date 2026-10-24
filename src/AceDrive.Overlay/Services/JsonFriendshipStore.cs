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
    private readonly string _file;
    private readonly ILog _log;
    private readonly Dictionary<string, int> _levels;

    public JsonFriendshipStore(ILog log)
        : this(DataPaths.UserSave("friendship.json"), log)
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
        // A failed bank must never break gameplay or shutdown; the next ride
        // retries with fresher numbers.
        if (!JsonFile.TryWrite(_file, _levels, out string? error))
        {
            _log.LogWarning(
                $"friendship.json unwritable at {_file} ({error}); progress kept in memory");
        }
    }

    private static Dictionary<string, int> LoadLevels(string file, ILog log)
    {
        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        JsonReadStatus status = JsonFile.TryRead(file, out Dictionary<string, int>? raw, out string? error);
        if (status == JsonReadStatus.Missing)
        {
            return levels;
        }

        // A corrupt save degrades to strangers everywhere, never takes the
        // overlay down.
        if (raw is null)
        {
            log.LogWarning(
                $"friendship.json unreadable at {file} ({error}); starting strangers");
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

    private static string Normalize(string? ownerName) => (ownerName ?? "").Trim();
}
