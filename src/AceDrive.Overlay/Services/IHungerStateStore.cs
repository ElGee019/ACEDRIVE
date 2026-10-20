// Hunger persistence (Unity PlayerData.hunger survives restarts): the
// meter and its one-shot event resume when the cockpit reopens, written
// on quit like the city state.
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

public interface IHungerStateStore
{
    /// <summary>Saved reading, or null when there is nothing to resume.</summary>
    HungerState? Load();

    void Save(HungerState state);
}
