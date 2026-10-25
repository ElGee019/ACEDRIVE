namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// What stopped RESET GAME. The settings page turns the kind into its own
/// sentence, so the copy stays in the resx and this stays data — the wipe
/// cannot tell the player anything without it.
/// </summary>
public enum GameResetFailureKind
{
    /// <summary>No failure: the wipe completed and the replacement is starting.</summary>
    None,

    /// <summary>A save file is held open by another program.</summary>
    SaveInUse,

    /// <summary>Windows refused to delete a save file.</summary>
    SaveDenied,

    /// <summary>The saves are gone, but the replacement process would not start.</summary>
    Restart,

    /// <summary>No wipe or restart service was injected, so nothing was attempted.</summary>
    Unavailable,
}
