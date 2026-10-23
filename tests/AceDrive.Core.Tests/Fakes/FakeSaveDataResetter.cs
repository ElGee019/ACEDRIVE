using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Scriptable save wipe: records the call and reports success.</summary>
public sealed class FakeSaveDataResetter : ISaveDataResetter
{
    public bool Result { get; set; } = true;
    public int Calls { get; private set; }

    public bool DeleteAll()
    {
        Calls++;
        return Result;
    }
}
