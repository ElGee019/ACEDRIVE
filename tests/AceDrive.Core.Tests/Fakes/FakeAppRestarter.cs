using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Scriptable restart: records the call instead of spawning a process.</summary>
public sealed class FakeAppRestarter : IAppRestarter
{
    public bool Result { get; set; } = true;
    public int Calls { get; private set; }

    public bool Restart()
    {
        Calls++;
        return Result;
    }
}
