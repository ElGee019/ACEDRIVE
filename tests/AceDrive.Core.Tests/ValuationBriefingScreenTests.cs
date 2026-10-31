using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The market valuation's briefing page runs headless (PORTPLAN 49): it is the
/// week's contract with the player, so the copy has to name what the jury
/// compares, the terms it scores and how long the entry stands — and both verbs
/// must reach the flow instead of deciding anything on the page.
/// </summary>
public sealed class ValuationBriefingScreenTests
{
    private sealed record Harness(
        ValuationBriefingScreenViewModel Vm, FakeClock Clock, VoiceBeat Beat,
        List<string> Events);

    private static Harness Create()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var events = new List<string>();
        var vm = new ValuationBriefingScreenViewModel(
            "Street 1 9",
            () => events.Add("start"),
            new RelayCommand(() => events.Add("back")),
            beat);
        return new Harness(vm, clock, beat, events);
    }

    [Fact]
    public void IntroLine_NamesThePickTheJuryTermsAndTheEntryWindow()
    {
        var h = Create();

        Assert.Contains(
            "vehicle of your choice that you own", h.Vm.IntroLine, StringComparison.Ordinal);
        Assert.Contains(
            "another player has already entered", h.Vm.IntroLine, StringComparison.Ordinal);
        // The score's terms: price, speed, power, jobs with this car, engine
        // health and the car's time trial standings.
        Assert.Contains("the price of your car", h.Vm.IntroLine, StringComparison.Ordinal);
        Assert.Contains("its top speed, power", h.Vm.IntroLine, StringComparison.Ordinal);
        Assert.Contains(
            "how many jobs you have completed with it", h.Vm.IntroLine, StringComparison.Ordinal);
        Assert.Contains("its engine health", h.Vm.IntroLine, StringComparison.Ordinal);
        Assert.Contains(
            "time trial standings with this car", h.Vm.IntroLine, StringComparison.Ordinal);
        // The two-day stand and the live comparison: the entry is not a one-off
        // verdict, it keeps answering other players' entries.
        Assert.Contains("two real-world days", h.Vm.IntroLine, StringComparison.Ordinal);
        Assert.Contains("other players' entries", h.Vm.IntroLine, StringComparison.Ordinal);
    }

    [Fact]
    public void StartAndBack_ReachTheirVerbs_AndAnswerByLabel()
    {
        var h = Create();

        Assert.Equal("START DRIVE", h.Vm.StartLabel);
        Assert.True(h.Vm.VoiceActions.ContainsKey("START DRIVE"));
        Assert.True(h.Vm.VoiceActions.ContainsKey("BACK"));

        h.Vm.StartCommand.Execute(null);
        h.Vm.BackCommand.Execute(null);

        Assert.Equal(["start", "back"], h.Events);
        Assert.Equal("Street 1 9", h.Vm.VenueLine);
    }

    [Fact]
    public void VoiceStart_GlowsThenActsOnTheSharedBeat()
    {
        var h = Create();

        h.Vm.VoiceActions["START DRIVE"]();

        // The mic's acknowledgment stays visible before the page leaves.
        Assert.True(h.Vm.StartFlash);
        Assert.Empty(h.Events);
        h.Clock.Advance(VoiceBeat.Duration);
        h.Beat.Poll();
        Assert.Equal(["start"], h.Events);
        Assert.False(h.Vm.StartFlash);
    }
}
