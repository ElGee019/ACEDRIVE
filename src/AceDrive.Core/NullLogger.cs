// No-op ILog sink. Lives beside the interface it implements (Core stays
// interface-only for real logging: the file sink is Overlay/Services).
namespace AceDrive.Core;

/// <summary>No-op sink for tests and for contexts where logging is unwanted.</summary>
public sealed class NullLogger : ILog
{
    public void LogInfo(string message) { }
    public void LogWarning(string message) { }
    public void LogError(string message) { }
}
