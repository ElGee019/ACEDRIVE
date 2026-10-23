using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Main control page runs headless: todo lines and voice actions
/// resolve from the stores and resx.</summary>
public sealed class MainControlScreenTests
{
    // FakeClock default: Monday 2026-10-12 12:00 UTC.
    private static MainControlScreenViewModel Create(
        FakeClock? clock = null, TodoTable? todos = null,
        bool deliveryOpen = false, TaxiShift? shift = null,
        bool taxiStartsShift = false, VoiceBeat? beat = null,
        ICommand? backCommand = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        FakeClock tick = clock ?? new FakeClock();
        TaxiShift shiftState = shift ?? new TaxiShift();
        return new MainControlScreenViewModel(
            beat ?? new VoiceBeat(tick),
            todos ?? TodoTable.Default,
            shiftState,
            new ControlActions(
                taxiStartsShift
                    ? new RelayCommand(() => shiftState.Start(tick.UtcNow))
                    : new RelayCommand(() => { }),
                new RelayCommand(() => { }, () => deliveryOpen),
                new RelayCommand(() => { })),
            backCommand ?? new RelayCommand(() => { }));
    }

    [Fact]
    public void TodoLines_StartWithMaxGrocery()
    {
        var vm = Create();
        Assert.Equal(4, vm.TodoLines.Count);
        Assert.Contains("Max's", vm.TodoLines[0], StringComparison.Ordinal);
        Assert.Contains("3", vm.TodoLines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_IncludesAllButtons()
    {
        var vm = Create(deliveryOpen: false);
        Assert.True(vm.VoiceActions.ContainsKey("START TAXI SHIFT"));
        Assert.True(vm.VoiceActions.ContainsKey("DELIVERY JOB"));
        Assert.True(vm.VoiceActions.ContainsKey("CAR HAULING JOB"));
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));
    }

    [Fact]
    public void Back_Click_ReturnsToTheMenu()
    {
        // The page's one way out: the injected command owns the navigation
        // (the screen controller opens the menu), the page only forwards it.
        int back = 0;
        var vm = Create(backCommand: new RelayCommand(() => back++));
        vm.BackCommand.Execute(null);
        Assert.Equal(1, back);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void Voice_BackTap_GlowsThenWaitsOutTheBeatBeforeNavigating()
    {
        // BACK navigates, so its voice tap glows teal for the shared beat
        // first and the page changes only when the beat is due — the same
        // contract as the delivery page's navigating tap.
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        int back = 0;
        var vm = Create(clock, beat: beat,
            backCommand: new RelayCommand(() => back++));

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.Equal(0, back); // the page has not flipped yet

        clock.Advance(TimeSpan.FromMilliseconds(499));
        beat.Poll();
        Assert.Equal(0, back); // still inside the beat

        clock.Advance(TimeSpan.FromMilliseconds(2));
        beat.Poll(); // what the 10 Hz poll does at the beat's end
        Assert.Equal(1, back);
        Assert.False(vm.BackFlash); // the glow ends with the tap
    }

    [Fact]
    public void VoiceFlash_ClearsAfterHalfSecond()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        vm.VoiceActions["START TAXI SHIFT"].Invoke();
        Assert.True(vm.TaxiFlash);
        clock.Advance(TimeSpan.FromMilliseconds(499));
        vm.UpdateFlash();
        Assert.True(vm.TaxiFlash);
        clock.Advance(TimeSpan.FromMilliseconds(2));
        vm.UpdateFlash();
        Assert.False(vm.TaxiFlash);
    }

    [Fact]
    public void Voice_DeliveryTap_GlowsThenWaitsOutTheBeatBeforeNavigating()
    {
        // The page change waits so the teal acknowledgment stays visible:
        // the tap lights DELIVERY JOB for one half-second beat, and only
        // then does the injected command open the delivery career page.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool fired = false;
        var vm = new MainControlScreenViewModel(
            beat, TodoTable.Default,
            new TaxiShift(),
            new ControlActions(
                new RelayCommand(() => { }),
                new RelayCommand(() => fired = true),
                new RelayCommand(() => { })),
            new RelayCommand(() => { }));

        vm.VoiceActions["DELIVERY JOB"].Invoke();
        Assert.True(vm.DeliveryFlash);
        Assert.False(fired); // the page has not flipped yet

        clock.Advance(TimeSpan.FromMilliseconds(499));
        beat.Poll();
        Assert.False(fired); // still inside the beat

        clock.Advance(TimeSpan.FromMilliseconds(2));
        beat.Poll(); // what the 10 Hz poll does at the beat's end
        Assert.True(fired);
        Assert.False(vm.DeliveryFlash); // the glow ends with the tap
    }

    // The car-hauling entry is a placeholder (PORTPLAN 20 has no flow yet):
    // a tap only answers with the shared teal glow, so the glow is the whole
    // observable contract. Every spoken slice must reach the same button.
    [Fact]
    public void Voice_HaulTap_GlowsThenClearsAfterHalfSecond()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        vm.VoiceActions["CAR HAULING JOB"].Invoke();
        Assert.True(vm.HaulFlash);
        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.UpdateFlash();
        Assert.False(vm.HaulFlash);
    }

    [Theory]
    [InlineData("car")]
    [InlineData("car hauling")]
    [InlineData("car hauling job")]
    public void Voice_HaulAnswersToEverySpokenSlice(string spoken)
    {
        var vm = Create();
        string? label = VoiceMatch.MatchLabel(spoken, vm.VoiceActions.Keys);
        Assert.NotNull(label);
        vm.VoiceActions[label].Invoke();
        Assert.True(vm.HaulFlash);
    }

    [Fact]
    public void Voice_OffersOpenDelivery()
    {
        var vm = Create(deliveryOpen: true);
        Assert.True(vm.VoiceActions.ContainsKey("DELIVERY JOB"));
    }

    [Fact]
    public void TaxiLabel_FollowsShiftState()
    {
        var shift = new TaxiShift();
        var vm = Create(shift: shift);
        Assert.Equal("START TAXI SHIFT", vm.TaxiLabel);
        shift.Start(new FakeClock().UtcNow);
        vm.Refresh();
        Assert.Equal("CONTINUE TAXI SHIFT", vm.TaxiLabel);
        Assert.True(vm.VoiceActions.ContainsKey("CONTINUE TAXI SHIFT"));
    }

    [Fact]
    public void Voice_TaxiTap_HoldsStartLabelUntilFlashClears()
    {
        var clock = new FakeClock();
        var vm = Create(clock, taxiStartsShift: true);
        vm.VoiceActions["START TAXI SHIFT"].Invoke();
        vm.Refresh(); // what MainViewModel.OpenTaxiShift does mid-command in the app
        Assert.True(vm.TaxiFlash);
        Assert.Equal("START TAXI SHIFT", vm.TaxiLabel); // glows, name unchanged

        clock.Advance(TimeSpan.FromSeconds(0.51)); // past the half-second beat
        vm.UpdateFlash();
        Assert.False(vm.TaxiFlash);
        Assert.Equal("CONTINUE TAXI SHIFT", vm.TaxiLabel);
    }

    [Fact]
    public void TaxiClick_NoFlash_FlipsLabelAtOnce()
    {
        var vm = Create(taxiStartsShift: true);
        vm.TaxiCommand.Execute(null);
        vm.Refresh(); // real wiring repaints inside the taxi tap
        Assert.Equal("CONTINUE TAXI SHIFT", vm.TaxiLabel);
    }

    [Fact]
    public void Voice_BothShiftLabels_AnswerInEitherState()
    {
        var vm = Create();
        Assert.True(vm.VoiceActions.ContainsKey("START TAXI SHIFT"));
        Assert.True(vm.VoiceActions.ContainsKey("CONTINUE TAXI SHIFT"));

        // "CONTINUE TAXI SHIFT" fires the taxi entry even while the
        // button still reads START (shift closed).
        var clock = new FakeClock();
        var shift = new TaxiShift();
        var vm2 = Create(clock, shift: shift, taxiStartsShift: true);
        vm2.VoiceActions["CONTINUE TAXI SHIFT"].Invoke();
        Assert.True(shift.IsStarted);
        Assert.True(vm2.TaxiFlash);
    }

    [Fact]
    public void Voice_StartTaxi_TapsTaxiEntry()
    {
        // The request from the logged 2026-10-14 sessions: two spoken words
        // fire what three words print, beside the existing triggers.
        var clock = new FakeClock();
        var shift = new TaxiShift();
        var vm = Create(clock, shift: shift, taxiStartsShift: true);
        Assert.True(vm.VoiceActions.ContainsKey("start taxi"));

        vm.VoiceActions["start taxi"].Invoke();
        Assert.True(shift.IsStarted);
        Assert.True(vm.TaxiFlash);
    }

    [Fact]
    public void Voice_ContinueShortcut_FiresTaxi()
    {
        // One word, not the whole label: "continue" taps the taxi button.
        var clock = new FakeClock();
        var shift = new TaxiShift();
        var vm = Create(clock, shift: shift, taxiStartsShift: true);
        Assert.True(vm.VoiceActions.ContainsKey("continue"));
        vm.VoiceActions["continue"].Invoke();
        Assert.True(shift.IsStarted);
        Assert.True(vm.TaxiFlash);
    }

    [Fact]
    public void Voice_NoLongerOffersTheBusinessDoors()
    {
        // The bank/dealer/wash/mechanic/insurance entries left the jobs page:
        // they are POIs on the addresses page now (PORTPLAN 24/59).
        var vm = Create();
        Assert.DoesNotContain("bank", vm.VoiceActions.Keys);
        Assert.DoesNotContain("dealer", vm.VoiceActions.Keys);
        Assert.DoesNotContain("wash", vm.VoiceActions.Keys);
        Assert.DoesNotContain("mechanic", vm.VoiceActions.Keys);
        Assert.DoesNotContain("insurance", vm.VoiceActions.Keys);
    }
}
