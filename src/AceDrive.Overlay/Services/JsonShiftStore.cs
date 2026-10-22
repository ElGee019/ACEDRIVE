using System.IO;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Loads/saves the taxi shift under %AppData%\AceDrive. Never throws: a
/// missing or corrupt file means a closed shift, and a failed write must never
/// break gameplay or shutdown. The wall-clock stamps inside are what make the
/// idle timeout survive a restart.
/// </summary>
public sealed class JsonShiftStore : IShiftStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _file;
    private readonly ILog _log;

    public JsonShiftStore(ILog log)
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "shift.json"), log)
    { }

    public JsonShiftStore(string file, ILog log)
    {
        _file = file;
        _log = log;
    }

    public TaxiShiftSnapshot? Load()
    {
        try
        {
            if (!File.Exists(_file)) return null;
            TaxiShiftSnapshot? state = JsonSerializer.Deserialize<TaxiShiftSnapshot>(
                File.ReadAllText(_file), JsonOptions);
            if (state is null || !IsValid(state))
            {
                _log.LogWarning($"shift.json invalid at {_file}; starting with a closed shift");
                return null;
            }
            _log.LogInfo($"shift loaded: {(state.IsStarted ? "open" : "closed")}, " +
                $"{state.JobsDone} jobs / {state.Earned:0.00}");
            return state;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Corrupt or locked shift file just means a closed shift: the next
            // START opens a fresh one instead of blocking startup.
            _log.LogWarning($"shift.json unreadable at {_file} ({ex.GetType().Name}); starting with a closed shift");
            return null;
        }
    }

    public void Save(TaxiShiftSnapshot snapshot)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.WriteAllText(_file, JsonSerializer.Serialize(snapshot, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // never break gameplay or shutdown for persistence
            _log.LogWarning($"shift.json unwritable at {_file} ({ex.GetType().Name})");
        }
    }

    // An open shift without stamps can never resolve its timeout, so it is a
    // corrupt save rather than a resumable one.
    private static bool IsValid(TaxiShiftSnapshot state) =>
        state.JobsDone >= 0
        && state.Earned >= 0.0
        && double.IsFinite(state.Earned)
        && (!state.IsStarted || (state.StartedAt != default && state.LastJobEnd != default));
}
