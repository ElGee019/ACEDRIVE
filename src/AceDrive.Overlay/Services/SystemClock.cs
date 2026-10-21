using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Production clock for the composition root. Core stays dependency-free
/// (logic consumes <see cref="IClock"/>); this class is the wall-time end of
/// that seam, so it lives with the other system-boundary services.
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
