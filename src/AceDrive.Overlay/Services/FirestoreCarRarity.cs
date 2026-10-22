using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Firestore-backed car rarity: the global km board loaded at boot, then
/// refreshed after every owned-car trip. The immutable
/// <see cref="CarRarityTable"/> is swapped atomically, so the UI thread can
/// keep reading while a background load lands. Failures keep the last table.
/// </summary>
public sealed class FirestoreCarRarity : ICarRaritySource
{
    private readonly FirestoreCars _cars;
    private readonly ILog _log;
    private readonly CancellationToken _stopToken;
    private CarRarityTable _table = CarRarityTable.Empty;
    private int _loading;
    private bool _pushWarning;

    public FirestoreCarRarity(
        FirestoreCars cars, ILog log, CancellationToken stopToken)
    {
        _cars = cars;
        _log = log;
        _stopToken = stopToken;
    }

    public int CarCount => Volatile.Read(ref _table).CarCount;

    public int GetRarity(string? carKey) => Volatile.Read(ref _table).Rarity(carKey);

    public int GetKm(string? carKey) => Volatile.Read(ref _table).Km(carKey);

    /// <summary>Starts the boot load without blocking startup.</summary>
    public void Start() => SafeTask.Forget(RefreshAsync(), _log, "car rarity board");

    /// <summary>Reloads the board now (cold paths/tests); a no-op while loading.</summary>
    public Task RefreshAsync() => LoadAsync();

    public void RecordKm(string carKey, int km) =>
        SafeTask.Forget(RecordKmAsync(carKey, km), _log, "car km push");

    /// <summary>The write behind <see cref="RecordKm"/>, awaitable on cold paths.</summary>
    public Task RecordKmAsync(string carKey, int km)
    {
        if (string.IsNullOrWhiteSpace(carKey) || km <= 0)
        {
            return Task.CompletedTask;
        }
        return RecordAsync(carKey, km);
    }

    private async Task RecordAsync(string carKey, int km)
    {
        bool written = await _cars.IncrementKmAsync(carKey, km, _stopToken)
            .ConfigureAwait(false);
        if (!written)
        {
            if (!_pushWarning)
            {
                _pushWarning = true;
                _log.LogWarning($"firebase: car km push failed for {carKey}; keeping cached rarity");
            }
            return;
        }
        _pushWarning = false;
        await LoadAsync().ConfigureAwait(false);
    }

    private async Task LoadAsync()
    {
        if (Interlocked.CompareExchange(ref _loading, 1, 0) != 0)
        {
            return;
        }
        try
        {
            IReadOnlyList<CarKm>? rows = await _cars.LoadAsync(_stopToken)
                .ConfigureAwait(false);
            if (rows is null)
            {
                _log.LogWarning("firebase: car rarity board unavailable");
                return;
            }
            CarRarityTable table = CarRarityTable.From(rows);
            Volatile.Write(ref _table, table);
            _log.LogInfo($"firebase: car rarity board ready ({table.CarCount} cars)");
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }
}
