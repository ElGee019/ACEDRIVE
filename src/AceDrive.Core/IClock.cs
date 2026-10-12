// Core stays dependency-free: time flows through this interface so logic is
// deterministic and testable (FakeClock in tests, SystemClock in production).
// NOTE: deliberately no static Instance — stateful services are composed in
// the App root, never reached through globals (AGENTS.md: no singletons).
namespace AceDrive.Core;

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
