using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The breakdown page (PORTPLAN item 39, port of Unity BreakdownPopup):
/// the per-kind message, the stranded-engine variant, the tow-fee line and the
/// voice flow.</summary>
public sealed class BreakdownScreenTests
{
    private static BreakdownScreenViewModel New(BreakdownOutcome outcome, VoiceBeat? beat = null, Action? onBack = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        return new BreakdownScreenViewModel(
            outcome,
            beat ?? new VoiceBeat(new FakeClock()),
            new RelayCommand(() => onBack?.Invoke()));
    }

    private static BreakdownOutcome Outcome(BreakdownKind kind, bool engineFailure = false) =>
        new(kind, Fraction: 0.5, PickupFee: 100.0, engineFailure);

    [Theory]
    [InlineData(BreakdownKind.Taxi, "Your car broke down. The trip is cancelled.")]
    [InlineData(BreakdownKind.Delivery, "Your car broke down. The delivery is cancelled.")]
    [InlineData(BreakdownKind.Freeroam, "Your car broke down on the road.")]
    public void Message_FollowsTheKind(BreakdownKind kind, string expected) =>
        Assert.Equal(expected, New(Outcome(kind)).Message);

    [Fact]
    public void EngineFailure_ShowsTheStrandedMessageAndHint()
    {
        var vm = New(Outcome(BreakdownKind.Taxi, engineFailure: true));

        Assert.Contains("engine has failed", vm.Message);
        Assert.True(vm.FailureVisible);
        Assert.True(vm.HintVisible);
    }

    [Theory]
    [InlineData(100.0, true)]
    [InlineData(0.0, false)]
    public void FeeLine_FollowsThePickupFee(double fee, bool visible)
    {
        var vm = New(new BreakdownOutcome(BreakdownKind.Freeroam, Fraction: 0.0, PickupFee: fee, EngineFailure: false));
        Assert.Equal(visible, vm.FeeVisible);
    }

    [Fact]
    public void Voice_BackGlowsThenNavigates()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool navigated = false;
        var vm = New(Outcome(BreakdownKind.Taxi), beat, () => navigated = true);

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(navigated); // the glow shows before the page flips

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(navigated);
        Assert.False(vm.BackFlash);
    }
}
