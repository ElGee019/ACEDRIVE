using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the game moment under %AppData%\AceDrive. Never throws:
/// a missing or corrupt file means a fresh Saturday morning, and a failed
/// write must never break gameplay or shutdown.
/// </summary>
public sealed class JsonGameClockStore : IGameClockStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonGameClockStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "gameclock.json"), log)
    { }

    public JsonGameClockStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public GameClockState? Load()
    {
        try
        {
            if (!File.Exists(_file)) return null;
            GameClockState? state = JsonSerializer.Deserialize<GameClockState>(
                File.ReadAllText(_file), JsonOptions);
            if (state is null || state.Week < 1 || state.GameNow == default)
            {
                _log.LogWarning($"gameclock.json invalid at {_file}; starting a fresh week");
                return null;
            }
            _log.LogInfo($"clock loaded: {state.GameNow:yyyy-MM-dd HH:mm} week {state.Week}");
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Corrupt or locked clock file just means a fresh week: the game
            // moment rebuilds from the default start instead of blocking startup.
            _log.LogWarning($"gameclock.json unreadable at {_file} ({ex.GetType().Name}); starting a fresh week");
            return null;
        }
    }

    public void Save(GameClockState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // never break gameplay or shutdown for persistence
            _log.LogWarning($"gameclock.json unwritable at {_file} ({ex.GetType().Name})");
        }
    }
}
