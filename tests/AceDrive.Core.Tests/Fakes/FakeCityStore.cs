using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeCityStore : ICityStore
{
    public readonly List<IReadOnlyList<StoreState>> Saved = new();
    private readonly IReadOnlyList<StoreState>? _load;

    public FakeCityStore(IReadOnlyList<StoreState>? load = null) => _load = load;

    public IReadOnlyList<StoreState>? Load() => _load;
    public void Save(IReadOnlyList<StoreState> states) => Saved.Add(states);
}
