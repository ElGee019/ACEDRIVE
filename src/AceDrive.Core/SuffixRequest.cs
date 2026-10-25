// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity's passive customer asks).
namespace AceDrive.Core;

/// <summary>
/// One suffix-triggered passive ask: the line the drawn passenger says and
/// what it costs the tip. The condition default is empty, so a line that only
/// asks "accept or refuse" needs no content beyond its text.
/// </summary>
public sealed record SuffixRequest
{
    public string Text { get; init; } = "";

    public RideCondition Condition { get; init; } = RideCondition.None;
}
