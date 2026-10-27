using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// Scriptable weekly valuation list: the entries the next read answers, the
/// pushes and wipes the flow made, and a live watcher the test can fire. The
/// online flag lets a case pin the null-object behaviour without swapping the
/// fake.
/// </summary>
internal sealed class FakeValuationBoard : IValuationBoard
{
    /// <summary>What <see cref="ListAsync"/> answers; null simulates a failed read.</summary>
    public IReadOnlyList<ValuationEntry>? Entries { get; set; } = [];

    /// <summary>The anonymous uid the flow compares against.</summary>
    public string? OwnUid { get; set; } = "me";

    public bool IsOnline { get; set; } = true;

    /// <summary>Answer of the next push: false keeps the entry local.</summary>
    public bool PushSucceeds { get; set; } = true;

    public int ListCalls { get; private set; }

    public int Wipes { get; private set; }

    public bool Watching { get; private set; }

    public List<(string PlayerName, string CarKey, int Jobs, double Broken, double Score)> Pushes
    { get; } = [];

    public event Action<IReadOnlyList<ValuationEntry>>? Changed;

    public Task<IReadOnlyList<ValuationEntry>?> ListAsync(
        CancellationToken cancellationToken = default)
    {
        ListCalls++;
        return Task.FromResult(Entries);
    }

    public Task<bool> PushEntryAsync(
        string playerName, string carKey, int jobsWithCar,
        double engineBrokenPercent, double score,
        CancellationToken cancellationToken = default)
    {
        Pushes.Add((playerName, carKey, jobsWithCar, engineBrokenPercent, score));
        return Task.FromResult(PushSucceeds);
    }

    public Task<bool> WipeOwnAsync(CancellationToken cancellationToken = default)
    {
        Wipes++;
        return Task.FromResult(true);
    }

    public void StartWatching() => Watching = true;

    public void StopWatching() => Watching = false;

    /// <summary>Lands a snapshot the way the background watcher does.</summary>
    public void RaiseChanged(IReadOnlyList<ValuationEntry> entries)
    {
        Entries = entries;
        Changed?.Invoke(entries);
    }
}
