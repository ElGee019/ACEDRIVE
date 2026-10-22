using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeShiftStore : IShiftStore
{
    public readonly List<TaxiShiftSnapshot> Saved = new();
    private readonly TaxiShiftSnapshot? _load;

    public FakeShiftStore(TaxiShiftSnapshot? load = null) => _load = load;

    public TaxiShiftSnapshot? Load() => _load;
    public void Save(TaxiShiftSnapshot snapshot) => Saved.Add(snapshot);
}
