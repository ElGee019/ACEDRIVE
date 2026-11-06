// Pure gameplay data. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate it.
namespace AceDrive.Core;

/// <summary>
/// One visit to grandma's table: the appetite reading before and after the
/// plate, plus the stack she packs for the road. The receipt page prints these
/// numbers; the flow logs them. Pure data, no sentences.
/// </summary>
public sealed record PancakeMeal(int Before, int After)
{
    /// <summary>The hunger points the plate took off the meter.</summary>
    public int Served => Before - After;
}
