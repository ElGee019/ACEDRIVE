namespace AceDrive.Overlay.Services;

/// <summary>
/// The outcome of a save wipe. Naming the first blocked file is the whole
/// point: a bare false left the player with "close AceDrive and try again"
/// when the honest answer was "driver.json is held open, or Windows refused
/// the delete", which is what the settings page now has to say.
/// </summary>
public readonly record struct SaveResetReport(string? BlockedFile, SaveResetBlock Block)
{
    /// <summary>Every career file is gone; nothing to explain.</summary>
    public static readonly SaveResetReport Completed = new(null, SaveResetBlock.None);

    public bool Complete => Block == SaveResetBlock.None;
}
