using System.IO;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Where the overlay's two file families live. Content ships beside the exe
/// (<c>Data/*.json</c>, rewriteable while the game runs); progress belongs to
/// the player under <c>%AppData%\AceDrive</c>. Kept here so no store spells a
/// path out itself and a future move is one edit, not thirty.
/// </summary>
internal static class DataPaths
{
    /// <summary>A shipped content file beside the exe.</summary>
    internal static string GameData(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Data", fileName);

    /// <summary>The per-player save folder; save-data reset wipes its entries.</summary>
    internal static string UserDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AceDrive");

    /// <summary>A per-player save file under %AppData%\AceDrive.</summary>
    internal static string UserSave(string fileName) =>
        Path.Combine(UserDirectory, fileName);

    /// <summary>
    /// A machine-local file under %LocalAppData%\AceDrive: credentials and
    /// caches that must not follow a roaming profile to another PC.
    /// </summary>
    internal static string LocalSave(string fileName) =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AceDrive",
            fileName);
}
