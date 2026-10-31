using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The Touristenfahrten briefing page runs headless: it is the run's pitch to
/// the player, so the copy has to name the snack-hungry crowd, the corner
/// spots it pays out on and the race-weekend bonus — and both buttons must
/// reach the flow instead of deciding anything on the page. The title is handed
/// in (the real-world event's name while one runs, the run's own otherwise), so
/// the page paints it verbatim.
/// </summary>
public sealed class TouristenBriefingScreenTests
{
    private sealed record Harness(
        TouristenBriefingScreenViewModel Vm, FakeClock Clock, VoiceBeat Beat,
        List<string> Events);

    private static Harness Create(string? title = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var events = new List<string>();
        var vm = new TouristenBriefingScreenViewModel(
            title ?? Strings.Get("TouristenTitle"),
            () => events.Add("start"),
            new RelayCommand(() => events.Add("back")),
            beat);
        return new Harness(vm, clock, beat, events);
    }

    [Fact]
    public void IntroLine_NamesTheSnackCrowdTheCornersAndTheBonus()
    {
        var h = Create();

        Assert.Contains(
            "in urgent need of their favorite snacks", h.Vm.IntroLine, StringComparison.Ordinal);
        Assert.Contains(
            "extra bonuses on actual race weekends", h.Vm.IntroLine, StringComparison.Ordinal);
        // The corners are the run's own spectator spots, not a generic destination.
        Assert.Contains("Brünnchen", h.Vm.IntroLine, StringComparison.Ordinal);
        Assert.Contains("Breidscheid Bridge", h.Vm.IntroLine, StringComparison.Ordinal);
        Assert.Contains("Do your research!", h.Vm.IntroLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Title_IsTheOrdinaryWeekendTitle_OrTheEventItIsHanded()
    {
        // The flow resolves the title: an ordinary day reads as the run's own
        // weekend, a race date as that event's name.
        Assert.Equal("TOURISTENFAHRTEN WEEKEND", Create().Vm.Title);
        Assert.Equal("NLS Round 1", Create("NLS Round 1").Vm.Title);
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
