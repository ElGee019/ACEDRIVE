using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Counts the panel, briefing, receipt and calendar edges of a Touristenfahrten run.</summary>
internal sealed class FakeTouristenHost : ITouristenfahrtenHost
{
    public int HidePanelCalls { get; private set; }

    public int CalendarCalls { get; private set; }

    public int RefreshStandingsCalls { get; private set; }

    public List<object> Briefings { get; } = [];

    public List<object> Receipts { get; } = [];

    public void HidePanel() => HidePanelCalls++;

    public void ShowBriefing(object screen) => Briefings.Add(screen);

    public void ShowReceipt(object screen) => Receipts.Add(screen);

    public void GoToCalendar() => CalendarCalls++;

    public void RefreshStandings() => RefreshStandingsCalls++;
}
