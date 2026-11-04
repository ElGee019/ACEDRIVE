// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// One settled fill, handed from the tracker to the receipt flow.
namespace AceDrive.Core;

/// <summary>How many litres EVO's pump added in one fill (PORTPLAN item 37).
/// The level delta is the only thing the game gives us — the price is ours.</summary>
public sealed record FuelFill(double Litres);
