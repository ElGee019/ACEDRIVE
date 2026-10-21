// One planned speed-challenge patrol (PORTPLAN item 11, Phase C). Pure data:
// the host logs it, the HUD warns by offset, the fine check compares speed.
namespace AceDrive.Core;

/// <summary>
/// A police patrol pinned to a door on the placeholder loop. The lap offset is
/// resolved at the runtime track length when the plan is drawn, so the fine
/// check never needs the address map.
/// </summary>
public sealed record PolicePatrol(
    int Id,
    AddressLocation Address,
    double LapOffsetMeters,
    double RadiusMeters,
    int SpeedLimitKmh);
