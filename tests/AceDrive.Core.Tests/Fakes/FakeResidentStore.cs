using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>In-memory IResidentStore: substitutes for residents.json without
/// touching disk, and exposes the save count for persistence assertions.</summary>
internal sealed class FakeResidentStore : IResidentStore
{
    private readonly List<Resident> _residents = [];

    public FakeResidentStore(IEnumerable<Resident>? residents = null)
    {
        if (residents is not null)
        {
            _residents.AddRange(residents);
        }
    }

    public int Saves { get; private set; }

    public IReadOnlyList<Resident> Residents => _residents;

    public IReadOnlyList<Resident> Load() => [.. _residents];

    public void Save(IReadOnlyList<Resident> residents)
    {
        _residents.Clear();
        _residents.AddRange(residents);
        Saves++;
    }
}
