using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The coordinator a navigator reports to: records the shift that timed out,
/// how often the readouts were re-read, and which of the two non-page entries
/// (settings, haul) the menu asked for.
/// </summary>
internal sealed class FakeNavigationHost : INavigationHost
{
    public TimeSpan? Idle { get; private set; }

    public int ShiftTimeouts => Idle is null ? 0 : 1;

    public int Readouts { get; private set; }

    public int Settings { get; private set; }

    public int Hauls { get; private set; }

    public void ShiftTimedOut(TimeSpan idle) => Idle = idle;

    public void ReadoutsChanged() => Readouts++;

    public void ShowSettings() => Settings++;

    public void SettleHaul() => Hauls++;
}
