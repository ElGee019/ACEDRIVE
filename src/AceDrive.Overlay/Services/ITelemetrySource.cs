using System.Globalization;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Track metadata from EVO's static block. Co-located with
/// <see cref="ITelemetrySource"/> per the small-DTO-on-its-interface rule.
/// </summary>
public sealed record TrackInfo(
    string Name,
    string Configuration,
    double LengthMeters);

/// <summary>One physics tick. Plain data, no Win32 types. Co-located with
/// <see cref="ITelemetrySource"/> per the small-DTO-on-its-interface rule.
/// The optional fields stay null when EVO's graphics/static block is missing,
/// so the core speed/gear read keeps working on its own. <c>CarDamage</c> is
/// the sum of EVO's five collision-damage zones in the game's absolute units;
/// it only ever jumps on a hit (or drops on a repair/session reload).
/// <c>FuelLitres</c> is the tank level in the AC1-compatible fuel field
/// (litres); the fuel receipt prices the rise it sees, so a frame without the
/// field simply reports 0 and the tracker re-primes.</summary>
public sealed record TelemetrySample(
    int PacketId, float Gas, float Brake, int Gear,
    int Rpm, float SteerRad, float SpeedKmh,
    float? NormalizedPosition = null,
    TrackInfo? Track = null,
    float CarDamage = 0f,
    float FuelLitres = 0f);

/// <summary>
/// Abstraction over EVO shared memory. Lets ViewModels run against
/// <c>FakeTelemetrySource</c> in tests (LSP) and keeps every byte offset
/// inside <c>ShmReader</c>.
/// </summary>
public interface ITelemetrySource : IDisposable
{
    bool GameRunning { get; }
    bool PhysicsPresent { get; }
    TelemetrySample? TryRead();
}
