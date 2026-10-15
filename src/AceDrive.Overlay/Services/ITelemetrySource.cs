using System.Globalization;

namespace AceDrive.Overlay.Services;

/// <summary>One physics tick. Plain data, no Win32 types. Co-located with
/// <see cref="ITelemetrySource"/> per the small-DTO-on-its-interface rule.</summary>
public sealed record TelemetrySample(
    int PacketId, float Gas, float Brake, int Gear,
    int Rpm, float SteerRad, float SpeedKmh);

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
