using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The hunger explanation page: the resx copy, both verbs (CONTINUE runs the
/// continuation, TO TRUNK opens the trunk page) and the shared voice beat.
/// Copy is asserted through the resx, so a translator's edit is what the page
/// shows.
/// </summary>
public sealed class HungerIntroScreenViewModelTests
{
    // The copy resolves through the current UI culture: pin it to en-US like
    // every other page suite, or a Dutch machine would render the satellites.
    public HungerIntroScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture =
            new CultureInfo("en-US");

    private static HungerIntroScreenViewModel Create(
        Action? onContinue = null, Action? onToTrunk = null, VoiceBeat? beat = null) =>
        new(
            onContinue ?? (() => { }),
            new RelayCommand(onToTrunk ?? (() => { })),
            beat ?? new VoiceBeat(new FakeClock()));

    [Fact]
    public void Copy_ComesFromTheResx()
    {
        HungerIntroScreenViewModel vm = Create();

        Assert.Equal("GETTING HUNGRY", vm.Title);
        Assert.Equal(Strings.Get("HungerIntroTitle"), vm.Title);
        Assert.Equal(Strings.Get("HungerIntroBody"), vm.Body);
        Assert.StartsWith("As time progresses you will get hungry.", vm.Body, StringComparison.Ordinal);
        Assert.Equal("CONTINUE", vm.ContinueLabel);
        Assert.Equal("TO TRUNK", vm.ToTrunkLabel);
    }

    [Fact]
    public void Body_KeepsTheFourLineLayout_FoodThenTrunk()
    {
        // The layout is part of the request: four lines, two paragraphs, so a
        // translator reflows the words but never the breaks.
        string[] lines = Create().Body.Split('\n');

        Assert.Equal(5, lines.Length);
        Assert.Equal(string.Empty, lines[2]);
    }

    [Fact]
    public void Continue_RunsTheContinuation()
    {
        var continued = new bool[1];
        HungerIntroScreenViewModel vm = Create(onContinue: () => continued[0] = true);

        vm.ContinueCommand.Execute(null);

        Assert.True(continued[0]);
        Assert.False(vm.ContinueFlash);
    }

    [Fact]
    public void ToTrunk_OpensTheTrunk()
    {
        var opened = new bool[1];
        HungerIntroScreenViewModel vm = Create(onToTrunk: () => opened[0] = true);

        vm.ToTrunkCommand.Execute(null);

        Assert.True(opened[0]);
        Assert.False(vm.ToTrunkFlash);
    }

    [Fact]
    public void VoiceContinue_AnswersItsLabel_ThenRunsAfterTheBeat()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var continued = new bool[1];
        HungerIntroScreenViewModel vm = Create(
            onContinue: () => continued[0] = true, beat: beat);

        Assert.True(vm.VoiceActions.TryGetValue(
            Strings.Get("HungerIntroContinue"), out Action? tap));
        tap!();
        Assert.True(vm.ContinueFlash);
        Assert.False(continued[0]);

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();

        Assert.True(continued[0]);
    }

    [Fact]
    public void VoiceToTrunk_AnswersItsLabel_ThenOpensAfterTheBeat()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var opened = new bool[1];
        HungerIntroScreenViewModel vm = Create(
            onToTrunk: () => opened[0] = true, beat: beat);

        Assert.True(vm.VoiceActions.TryGetValue(
            Strings.Get("HungerIntroToTrunk"), out Action? tap));
        tap!();
        Assert.True(vm.ToTrunkFlash);
        Assert.False(opened[0]);

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();

        Assert.True(opened[0]);
    }

    [Fact]
    public void Click_DropsAPendingVoiceTap_SoTheContinuationRunsOnce()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        int continued = 0;
        HungerIntroScreenViewModel vm = Create(
            onContinue: () => continued++, beat: beat);

        vm.VoiceActions[Strings.Get("HungerIntroContinue")]();
        vm.ContinueCommand.Execute(null);
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();

        Assert.Equal(1, continued);
    }

    [Fact]
    public void Page_KeepsThePanelOpen_AndIsAVoiceScreen()
    {
        HungerIntroScreenViewModel vm = Create();

        Assert.IsAssignableFrom<IKeepPanelOpen>(vm);
        Assert.IsAssignableFrom<IVoiceScreen>(vm);
    }
}
