using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeTelemetry : ITelemetrySource
{
    public bool GameRunning { get; set; } = true;
    public bool PhysicsPresent { get; set; } = true;
    public TelemetrySample Next { get; set; } =
        new(1, 0.5f, 0f, 4, 5000, 0f, 90f);
    public void Dispose() { }
    public TelemetrySample? TryRead() => PhysicsPresent ? Next : null;
}
