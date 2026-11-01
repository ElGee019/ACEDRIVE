using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The car-mod suggestion page: one question, with BACK and SUGGEST
/// in the top band and the same teal voice acknowledgment as every other page.</summary>
public sealed class SuggestCarModScreenViewModelTests
{
    public SuggestCarModScreenViewModelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }

    private static (SuggestCarModScreenViewModel Vm, FakeClock Clock, VoiceBeat Beat) Create(
        Action? back = null, Action? suggest = null)
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        return (new SuggestCarModScreenViewModel(
            new RelayCommand(back ?? (() => { })),
            new RelayCommand(suggest ?? (() => { })),
            beat), clock, beat);
    }

    [Fact]
    public void Prompt_IsTheQuestionFromTheResources()
    {
        var (vm, _, _) = Create();
        Assert.Equal(
            "know/have a good car mod you would like to see added to the gameplay?",
            vm.Prompt);
    }

    [Fact]
    public void Voices_OfferBackAndSuggest()
    {
        var (vm, _, _) = Create();
        Assert.Contains("BACK", vm.VoiceActions.Keys);
        Assert.Contains("SUGGEST", vm.VoiceActions.Keys);
    }

    [Fact]
    public void ClickBack_ReturnsToTheHomePageAtOnce()
    {
        bool backed = false;
        var (vm, _, _) = Create(back: () => backed = true);

        vm.BackCommand.Execute(null);

        Assert.True(backed);
    }

    [Fact]
    public void ClickSuggest_ReportsTheSuggestionAtOnce()
    {
        bool suggested = false;
        var (vm, _, _) = Create(suggest: () => suggested = true);

        vm.SuggestCommand.Execute(null);

        Assert.True(suggested);
    }

    [Fact]
    public void VoiceBack_GlowsThenReturnsToTheHomePageAfterTheBeat()
    {
        bool backed = false;
        var (vm, clock, beat) = Create(back: () => backed = true);

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(vm.SuggestFlash);
        Assert.False(backed); // the acknowledgment owns the beat

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(backed);
        Assert.False(vm.BackFlash); // no stale glow to re-show later
    }

    [Fact]
    public void Click_ActsAtOnceAndDropsAPendingVoiceTap()
    {
        int attempted = 0;
        var (vm, clock, beat) = Create(suggest: () => attempted++);

        vm.VoiceActions["SUGGEST"].Invoke();
        vm.SuggestCommand.Execute(null);

        Assert.Equal(1, attempted);
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.Equal(1, attempted);
    }
}
