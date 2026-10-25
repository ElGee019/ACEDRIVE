using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The coordinator a navigator reports to: records the shift that timed out,
/// how often the readouts were re-read, and whether the menu asked for the
/// settings page. The haul needs no report: its receipt is the flow's page.
/// </summary>
internal sealed class FakeNavigationHost : INavigationHost
{
    public TimeSpan? Idle { get; private set; }

    public int ShiftTimeouts => Idle is null ? 0 : 1;

    public int Readouts { get; private set; }

    public int Settings { get; private set; }

    /// <summary>How many destinations the addresses page pinned.</summary>
    public int Pins { get; private set; }

    public void ShiftTimedOut(TimeSpan idle) => Idle = idle;

    public void ReadoutsChanged() => Readouts++;

    public void ShowSettings() => Settings++;

    public void DestinationPinned() => Pins++;
}
