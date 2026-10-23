using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>In-memory addresses file: seeds a known list and records saves.</summary>
internal sealed class FakePoiDirectoryStore : IPoiDirectoryStore
{
    private List<PointOfInterest> _saved = [];

    public FakePoiDirectoryStore(IEnumerable<PointOfInterest>? known = null) =>
        _saved = known is null ? [] : [.. known];

    public int Saves { get; private set; }

    public IReadOnlyList<PointOfInterest> Saved => _saved;

    public IReadOnlyList<PointOfInterest> Load() => _saved;

    public void Save(IReadOnlyList<PointOfInterest> pois)
    {
        Saves++;
        _saved = [.. pois];
    }
}
