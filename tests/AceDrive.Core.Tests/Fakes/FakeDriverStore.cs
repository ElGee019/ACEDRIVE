using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeDriverStore : IDriverStore
{
    public readonly List<DriverState> Saved = new();
    private readonly DriverState? _load;

    public FakeDriverStore(DriverState? load = null) => _load = load;

    public DriverState? Load() => _load;
    public void Save(DriverState state) => Saved.Add(state);
}
