namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The settings page's answer to RESET GAME: nothing when the replacement
/// process is starting, otherwise which file (or which step) stopped it. The
/// file name rides along so the page can point at the one thing to unblock
/// instead of telling the player to close the overlay that just refused.
/// </summary>
public readonly record struct GameResetFailure(GameResetFailureKind Kind, string? File = null)
{
    /// <summary>The reset is under way; this process is about to end.</summary>
    public static readonly GameResetFailure None = new(GameResetFailureKind.None);
}
