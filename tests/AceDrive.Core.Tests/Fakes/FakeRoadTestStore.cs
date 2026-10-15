using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeRoadTestStore : IRoadTestStore
{
    public readonly List<RoadTestSnapshot> Saved = new();
    private readonly RoadTestSnapshot? _load;

    public FakeRoadTestStore(RoadTestSnapshot? load = null) => _load = load;

    public RoadTestSnapshot? Load() => _load;
    public void Save(RoadTestSnapshot snapshot) => Saved.Add(snapshot);
}
