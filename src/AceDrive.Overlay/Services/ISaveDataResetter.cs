namespace AceDrive.Overlay.Services;

/// <summary>
/// Removes every file that holds a career: the introduction walk, the profile,
/// the wallet and its garage, the town, the game moment and the taxi books.
/// Settings and the Firebase session survive on purpose — the first is a
/// preference, and the second keeps the name already claimed by this player
/// reclaimable (Unity's same-uid "Owned" rule) after the reset.
/// </summary>
public interface ISaveDataResetter
{
    /// <summary>
    /// Deletes every save file. The report names the first file that survived,
    /// so the caller keeps the running session instead of restarting onto a
    /// half-wiped career — and can tell the player which file to unblock.
    /// </summary>
    SaveResetReport DeleteAll();
}
