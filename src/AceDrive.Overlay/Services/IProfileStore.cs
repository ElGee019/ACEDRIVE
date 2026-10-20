using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>Player identity behind an interface so views never touch the disk.</summary>
public interface IProfileStore
{
    PlayerProfile? Load();
    void Save(PlayerProfile profile);
}
