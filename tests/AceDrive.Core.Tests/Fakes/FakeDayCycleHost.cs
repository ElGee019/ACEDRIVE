using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The coordinator a day cycle reports to: the weeks it rolled, how often the
/// readouts were forced, the page of the shift that ended and its CONTINUE.
/// </summary>
internal sealed class FakeDayCycleHost : IDayCycleHost
{
    public List<int> Weeks { get; } = [];

    public int Readouts { get; private set; }

    public object? Page { get; private set; }

    public int MainPages { get; private set; }

    public void DisplayWeekChanged(int week) => Weeks.Add(week);

    public void ReadoutsChanged() => Readouts++;

    public void ShowShiftEnded(object page) => Page = page;

    public void GoToMainPage() => MainPages++;
}
