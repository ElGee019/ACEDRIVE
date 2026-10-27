using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Zone-standings backend with a scripted result: records the areas
/// the expansion page ranked and can raise the live-update event exactly like
/// a late board landing under the open page.</summary>
internal sealed class FakeZoneStandings : IZoneStandings
{
    private readonly List<double> _refreshedAt = [];

    /// <summary>What LastResult and Refresh hand out; null = nothing known.</summary>
    public ZoneStandingsResult? Result { get; set; }

    public IReadOnlyList<double> RefreshedAt => _refreshedAt;

    public ZoneStandingsResult? LastResult => Result;

    public bool IsAvailable { get; set; } = true;

    public int Starts { get; private set; }

    public event Action<ZoneStandingsResult>? ResultUpdated;

    public void Start() => Starts++;

    public void Refresh(double zoneKm)
    {
        _refreshedAt.Add(zoneKm);
    }

    /// <summary>Raises ResultUpdated like a fresh board result landing.</summary>
    public void Emit(ZoneStandingsResult result)
    {
        Result = result;
        ResultUpdated?.Invoke(result);
    }
}
