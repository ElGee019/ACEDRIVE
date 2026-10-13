using AceDrive.Core;

namespace AceDrive.Core.Tests;

internal sealed class FakeClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    public DateTime UtcNow => Now;
    public void Advance(TimeSpan dt) => Now += dt;
}
