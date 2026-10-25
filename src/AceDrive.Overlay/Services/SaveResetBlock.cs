namespace AceDrive.Overlay.Services;

/// <summary>
/// Why a save file survived RESET GAME's wipe. The two failures need opposite
/// advice — another program holding driver.json is closed by the player, while
/// a refused delete is a rights problem on the folder itself — so the wipe
/// reports which one it met instead of a bare false.
/// </summary>
public enum SaveResetBlock
{
    /// <summary>Nothing was blocked; the wipe completed.</summary>
    None,

    /// <summary>Another process still holds the file open (IOException).</summary>
    InUse,

    /// <summary>Windows refused the delete (UnauthorizedAccessException).</summary>
    Denied,
}
