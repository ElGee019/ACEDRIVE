// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/43.
namespace AceDrive.Core;

/// <summary>What a restart must reopen: the cash wallet and the XP total.</summary>
public sealed record DriverState(double Cash, double Xp)
{
    public static readonly DriverState Fresh = new(0.0, 0.0);
}
