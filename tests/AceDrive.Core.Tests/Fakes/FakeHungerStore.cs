using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeHungerStore : IHungerStateStore
{
    public readonly List<HungerState> Saved = new();
    private readonly HungerState? _load;

    public FakeHungerStore(HungerState? load = null) => _load = load;

    public HungerState? Load() => _load;
    public void Save(HungerState state) => Saved.Add(state);
}
