using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeGameClockStore : IGameClockStore
{
    public readonly List<GameClockState> Saved = new();
    private readonly GameClockState? _load;

    public FakeGameClockStore(GameClockState? load = null) => _load = load;

    public GameClockState? Load() => _load;
    public void Save(GameClockState state) => Saved.Add(state);
}
