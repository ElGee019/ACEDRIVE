using System.Globalization;

namespace AceDrive.Overlay.Services;

/// <summary>One physics tick. Plain data, no Win32 types.</summary>
public sealed record TelemetrySample(
    int PacketId, float Gas, float Brake, int Gear,
    int Rpm, float SteerRad, float SpeedKmh);

/// <summary>AC gear convention shared by all consumers.</summary>
public static class TelemetryConventions
{
    public static string GearDisplay(int gear) => gear switch
    {
        0 => "R",
        1 => "N",
        _ => (gear - 1).ToString(CultureInfo.InvariantCulture),
    };
}

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
