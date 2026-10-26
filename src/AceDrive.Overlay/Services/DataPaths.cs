using System.IO;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Where the overlay's two file families live. Content ships beside the exe
/// (<c>Data/*.json</c>, rewriteable while the game runs); progress belongs to
/// the player under <c>%AppData%\AceDrive</c> (or <c>AceDrive-Demo</c> in a
/// demo build). Kept here so no store spells a path out itself and a future
/// move is one edit, not thirty.
/// </summary>
internal static class DataPaths
{
    /// <summary>A shipped content file beside the exe.</summary>
    internal static string GameData(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Data", fileName);

    // WHY a build-time folder switch: a demo package must start at onboarding
    // even on a machine that already plays AceDrive, and must never read or
    // write that player's saves. A normal build keeps the original folder, so
    // existing saves stay exactly where they are.
#if ACEDRIVE_DEMO
    private const string ProfileFolder = "AceDrive-Demo";
#else
    private const string ProfileFolder = "AceDrive";
#endif

    /// <summary>The per-player save folder; save-data reset wipes its entries.</summary>
    internal static string UserDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        ProfileFolder);

    /// <summary>A per-player save file in the profile folder.</summary>
    internal static string UserSave(string fileName) =>
        Path.Combine(UserDirectory, fileName);

    /// <summary>
    /// A machine-local file under %LocalAppData% in the profile folder:
    /// credentials and caches that must not follow a roaming profile to
    /// another PC.
    /// </summary>
    internal static string LocalSave(string fileName) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProfileFolder,
            fileName);

    /// <summary>The dated log sink folder.</summary>
    internal static string UserLogs => Path.Combine(UserDirectory, "logs");
}
