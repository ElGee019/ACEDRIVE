// Town persistence (PORTPLAN item 25, Unity: StoreManager's storeStates are
// written on quit and never mid-session): the city size with the shelf state
// that drives it, so a restart reopens the same town instead of re-seeding
// every store at its starting value.
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

public interface ICityStore
{
    /// <summary>Saved cells, or null when there is nothing to resume.</summary>
    IReadOnlyList<StoreState>? Load();

    void Save(IReadOnlyList<StoreState> states);
}
