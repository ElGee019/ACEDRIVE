// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// What the photographer captured for the road test (PORTPLAN item 49): the car
// under test, the door the shot was taken at and the lap offset that prices
// the variety against last week's shoot.
namespace AceDrive.Core;

/// <summary>
/// One photoshoot of the road test: the car under test, the address the car was
/// parked at when the shutter fired, and the lap offset the variety is measured
/// from. <paramref name="TrackLengthMeters"/> is the live lap length the offset
/// belongs to, kept here so the receipt is priced from one consistent geometry
/// even when the game's static block changes later.
/// </summary>
public sealed record RoadTestShot(
    string CarName,
    string Street,
    int HouseNumber,
    double OffsetMeters,
    double TrackLengthMeters)
{
    /// <summary>True when the shot was taken at a resolvable door.</summary>
    public bool HasAddress => Street.Length > 0 && HouseNumber > 0;
}
