using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Main control page runs headless: todo lines, HUD readings and
/// voice actions resolve from the stores and resx.</summary>
public sealed class MainControlScreenTests
{
    // FakeClock default: Monday 2026-10-12 12:00 UTC.
    private static MainControlScreenViewModel Create(
        FakeClock? clock = null, TodoTable? todos = null,
        bool deliveryOpen = false, TaxiShift? shift = null,
        bool taxiStartsShift = false, VoiceBeat? beat = null,
        Hunger? hunger = null, Garage? garage = null,
        ICommand? carWashCommand = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        FakeClock tick = clock ?? new FakeClock();
        TaxiShift shiftState = shift ?? new TaxiShift();
        return new MainControlScreenViewModel(
            beat ?? new VoiceBeat(tick),
            todos ?? TodoTable.Default, hunger ?? new Hunger(HungerRules.Default),
            garage ?? new Garage([]),
            shiftState,
            new ControlActions(
                taxiStartsShift
                    ? new RelayCommand(() => shiftState.Start(tick.UtcNow))
                    : new RelayCommand(() => { }),
                new RelayCommand(() => { }, () => deliveryOpen),
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                carWashCommand ?? new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                new RelayCommand(() => { })));
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
            new Hunger(HungerRules.Default), new Garage([]),
            new TaxiShift(),
            new ControlActions(
                new RelayCommand(() => { }),
                new RelayCommand(() => fired = true),
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                new RelayCommand(() => { })));

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
    public void HungerLine_ShowsTheReadingWhereTheJobButtonsSit()
    {
        // Unity's map HUD prints HUNGER x/36 above the job entries; the port
        // prints it above the job buttons (PORTPLAN 26).
        var vm = Create();
        Assert.Equal("HUNGER 0/36", vm.HungerText);
        Assert.False(vm.HungerAmber);
    }

    [Fact]
    public void HungerLine_WearsTheAmberBand()
    {
        // Unity YouScreen colours the reading orange from h 16 to h 29.
        var clock = new FakeClock();
        DateTime start = clock.UtcNow;
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(start, onboarded: true, hasPancakes: false);
        for (int i = 0; i < 16; i++)
        {
            _ = hunger.AdvanceTo(start.AddHours(i + 1), onboarded: true, hasPancakes: false);
        }

        var vm = Create(clock, hunger: hunger);
        Assert.Equal("HUNGER 16/36", vm.HungerText);
        Assert.True(vm.HungerAmber);
    }

    [Fact]
    public void FilthLine_ShowsTheCurrentCarsDirt()
    {
        // Unity's MapScreen HUD prints FILTH x/25 beside the hunger line
        // (PORTPLAN 33); the reading is the car driven now.
        var vm = Create(garage: Dirty(0));
        Assert.Equal("FILTH 0/25", vm.FilthText);
        Assert.False(vm.FilthAmber);
        Assert.False(vm.FilthFullyDirty);
    }

    [Fact]
    public void FilthLine_WearsTheAmberBand()
    {
        // Unity MapScreen colours the reading orange from 15 up to the cap.
        var vm = Create(garage: Dirty(15));
        Assert.Equal("FILTH 15/25", vm.FilthText);
        Assert.True(vm.FilthAmber);
        Assert.False(vm.FilthFullyDirty);
    }

    [Fact]
    public void FilthLine_AtTheCapReadsFullyDirty()
    {
        // Unity paints the cap the shared mint instead of the amber.
        var vm = Create(garage: Dirty(25));
        Assert.Equal("FILTH 25/25", vm.FilthText);
        Assert.False(vm.FilthAmber);
        Assert.True(vm.FilthFullyDirty);
    }

    [Fact]
    public void Voice_CarWashAnswersItsLabelAndTheSpokenSlice()
    {
        // The temporary entry (PORTPLAN 33) answers to its text and to what
        // the mic hears short of it, like the bank/garage/dealer entries.
        bool opened = false;
        var vm = Create(carWashCommand: new RelayCommand(() => opened = true));
        Assert.True(vm.VoiceActions.ContainsKey("CAR WASH"));
        Assert.True(vm.VoiceActions.ContainsKey("wash"));
        vm.VoiceActions["wash"].Invoke();
        Assert.True(opened);
        Assert.True(vm.CarWashFlash);
    }

    private static Garage Dirty(int filth)
    {
        var car = new CarSpec("ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Passengers: 1);
        var garage = new Garage([car]);
        garage.Add(car, week: 1).Filth.Add(filth);
        return garage;
    }
}
