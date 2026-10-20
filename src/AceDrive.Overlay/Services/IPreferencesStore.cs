using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The player's first-run choices behind an interface so views never touch
/// the disk. A missing or corrupt file means the defaults (kilometres,
/// English) instead of a failed start.
/// </summary>
public interface IPreferencesStore
{
    PlayerPreferences Load();
    void Save(PlayerPreferences preferences);
}
