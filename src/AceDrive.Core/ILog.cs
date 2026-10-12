// Logging abstraction lives in Core (interface only) so pure logic can log
// without taking an I/O dependency. Sinks live in Overlay/Services.
namespace AceDrive.Core;

public interface ILog
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}

/// <summary>No-op sink for tests and for contexts where logging is unwanted.</summary>
public sealed class NullLogger : ILog
{
    public void LogInfo(string message) { }
    public void LogWarning(string message) { }
    public void LogError(string message) { }
}
