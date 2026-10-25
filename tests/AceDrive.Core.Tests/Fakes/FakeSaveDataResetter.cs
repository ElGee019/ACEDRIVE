using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Scriptable save wipe: records the call and answers with a report.</summary>
public sealed class FakeSaveDataResetter : ISaveDataResetter
{
    public SaveResetReport Report { get; set; } = SaveResetReport.Completed;
    public int Calls { get; private set; }

    public SaveResetReport DeleteAll()
    {
        Calls++;
        return Report;
    }
}
