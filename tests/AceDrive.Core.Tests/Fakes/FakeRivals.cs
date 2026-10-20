using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Standings backend with a scripted board result: records the
/// refresh/push calls the flows made and can raise the live-update event
/// exactly like a late board landing under an open receipt.</summary>
internal sealed class FakeRivals : IRivalsService
{
    private readonly List<double> _refreshedAt = [];
    private readonly List<(double Xp, string Name)> _pushed = [];

    /// <summary>What LastResult and Refresh hand out; null = nothing known.</summary>
    public RivalsResult? Result { get; set; }

    public IReadOnlyList<double> RefreshedAt => _refreshedAt;

    public IReadOnlyList<(double Xp, string Name)> Pushed => _pushed;

    /// <summary>Rank/total seen by the last RestoreSnapshot call.</summary>
    public (int Rank, int Total)? Restored { get; private set; }

    public RivalsResult? LastResult => Result;

    public event Action<RivalsResult>? ResultUpdated;

    public void RestoreSnapshot(DriverState? saved) =>
        Restored = saved is null ? null : (saved.RivalsRank, saved.RivalsTotal);

    public RivalsResult? Refresh(double playerXp)
    {
        _refreshedAt.Add(playerXp);
        return Result;
    }

    public void PushXp(double xp, string displayName) => _pushed.Add((xp, displayName));

    /// <summary>Raises ResultUpdated like a fresh board result landing.</summary>
    public void Emit(RivalsResult result)
    {
        Result = result;
        ResultUpdated?.Invoke(result);
    }
}
