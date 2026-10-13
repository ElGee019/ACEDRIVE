using AceDrive.Core;

namespace AceDrive.Core.Tests;

internal sealed class TestLog : ILog
{
    public readonly List<string> Warnings = new();
    public readonly List<string> Infos = new();
    public void LogInfo(string message) => Infos.Add(message);
    public void LogWarning(string message) => Warnings.Add(message);
    public void LogError(string message) { }
}
