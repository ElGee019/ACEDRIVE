// Logging abstraction lives in Core (interface only) so pure logic can log
// without taking an I/O dependency. Sinks live in Overlay/Services.
namespace AceDrive.Core;

public interface ILog
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
}
