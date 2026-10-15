// Driver persistence (PORTPLAN items 11/43, Unity PlayerData.cash + xp are
// written on quit and never mid-session): the cash wallet with the XP total,
// so a restart reopens the same driver instead of resetting every session.
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

public interface IDriverStore
{
    /// <summary>Saved driver, or null when there is nothing to resume.</summary>
    DriverState? Load();

    void Save(DriverState state);
}
