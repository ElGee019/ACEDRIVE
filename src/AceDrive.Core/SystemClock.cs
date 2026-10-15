// Production clock for the composition root. Core stays dependency-free:
// logic consumes IClock, this class is the wall-time end of that seam.
namespace AceDrive.Core;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
