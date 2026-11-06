using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Main control page runs headless: todo lines and voice actions
/// resolve from the stores and resx.</summary>
public sealed class MainControlScreenTests
{
    // The copy the haul flow hands the page for the brand-loyalty refusal. The
    // page prints whatever it is given (the flow owns the words), so the hold
    // tests pin these strings rather than the resx key.
    private const string LoyaltyGate = "NOT ENOUGH BRAND LOYALTY YET";
    // FakeClock default: Monday 2026-10-12 12:00 UTC.
    private static MainControlScreenViewModel Create(
        FakeClock? clock = null, TodoBoard? todos = null,
        bool deliveryOpen = false, TaxiShift? shift = null,
        bool taxiStartsShift = false, VoiceBeat? beat = null,
        ICommand? backCommand = null, bool canDeliverCarePackages = false,
        ICommand? deliverCarePackages = null, int week = 1)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        FakeClock tick = clock ?? new FakeClock();
        TaxiShift shiftState = shift ?? new TaxiShift();
        TodoBoard board = todos ?? DefaultTodoBoard();
        return new MainControlScreenViewModel(
            beat ?? new VoiceBeat(tick),
            tick,
            new GameClock(tick, tick.UtcNow),
            board,
            shiftState,
            new ControlActions(
                taxiStartsShift
                    ? new RelayCommand(() => shiftState.Start(tick.UtcNow))
                    : new RelayCommand(() => { }),
                new RelayCommand(() => { }, () => deliveryOpen),
                new RelayCommand(() => { }),
                deliverCarePackages ?? new RelayCommand(() => { }),
                () => canDeliverCarePackages,
                new RelayCommand(() => { }),
                () => RoadServiceOffer.Hidden),
            backCommand ?? new RelayCommand(() => { }),
            week);
    }

    // The real board over a fresh career: the four Unity starting tips, so the
    // page's formatting is the production one instead of a hand-made string.
    private static TodoBoard DefaultTodoBoard()
    {
        return new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), new PoiDirectory(),
            new FakeOnboardingStore(), new DriverWallet(), new JobLedger(),
            new OperatingZone(null, seed: 0, AddressMap.Default), new Garage(TestRoadCars.Pool),
            new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), new CarePackageLedger(),
            new Random(0), () => { }, new NullLogger());
    }

    // The real board over a given career: the boot sync flags whatever the
    // state already meets, so the page renders the production lines.
    private static TodoBoard Board(DriverState state)
    {
        return new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), new PoiDirectory(),
            new FakeOnboardingStore(), new DriverWallet(state), new JobLedger(state),
            new OperatingZone(null, seed: 0, AddressMap.Default), new Garage(TestRoadCars.Pool),
            new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), new CarePackageLedger(),
            new Random(0), () => { }, new NullLogger());
    }

    // The real board over a career that meets six tips and a town with one
    // shop: the boot sync grants one kit per completion (PORTPLAN 21), so the
    // page has a real bag threshold to read.
    private static TodoBoard CareBoard()
    {
        var directory = new PoiDirectory(
        [
            new PointOfInterest(
                "groceries_store", "Max's Grocery Store",
                new AddressLocation(
                    AddressMap.Default.Streets[0].Name, 1, HouseSide.Left),
                PoiTypes.GroceryStore),
        ]);
        var state = new DriverState(
            0.0, 2.0, Bank: 1002.0, CreditEarned: 500.0,
            TaxiJobsCompleted: 5, DeliveryJobsCompleted: 2, JobsCompleted: 18);
        return new TodoBoard(
            TodoTable.Default, new FakeTodoProgressStore(), directory,
            new FakeOnboardingStore(), new DriverWallet(state), new JobLedger(state),
            new OperatingZone(null, seed: 0, AddressMap.Default), new Garage(TestRoadCars.Pool),
            new VisitLedger(),
            new DeliveryCareer(HubCatalog.Default, 0, TestNames.For()), new CarePackageLedger(),
            new Random(0), () => { }, new NullLogger());
    }

    [Fact]
    public void Clock_ShowsTheMenusGameTimeAndDisplayWeek()
    {
        // The jobs page carries the same header as the menu and the calendar:
        // the design's 24-hour game time over the abbreviated weekday and the
        // display week, and it follows a roll like the other two.
        var clock = new FakeClock();
        var vm = Create(clock, week: 3);

        Assert.Equal("12:00", vm.ClockText);
        Assert.Equal("MON / WEEK 3", vm.DayWeekText);

        // One game hour: 60 minutes at the game clock's real-time rate, with a
        // second of slack for the double division (59.999 would stay 12:59).
        clock.Advance(TimeSpan.FromSeconds((GameClock.RealSecondsPerGameMinute * 60) + 1));
        vm.Refresh();
        Assert.Equal("13:00", vm.ClockText);

        vm.DisplayWeek = 4;
        Assert.Equal("MON / WEEK 4", vm.DayWeekText);
    }

    [Fact]
    public void TodoLines_RenderTheLedgersFourTips()
    {
        var vm = Create();
        Assert.Equal(4, vm.TodoLines.Count);
        Assert.StartsWith("Earn ", vm.TodoLines[0], StringComparison.Ordinal); // open tips stay unnumbered
        Assert.Contains("Earn 500 CR", vm.TodoLines[0], StringComparison.Ordinal);
        Assert.Contains("Earn 2 XP", vm.TodoLines[1], StringComparison.Ordinal);
        Assert.Contains("1002 CR in the bank", vm.TodoLines[2], StringComparison.Ordinal);
        Assert.Contains("Max's Grocery", vm.TodoLines[3], StringComparison.Ordinal);
    }

    [Fact]
    public void CompletedTodo_ListsTheKitButWithholdsDeliverWithoutFiveKits()
    {
        // One earned target is a listed kit, not a delivery: the explanation
        // shows, but DELIVER CARE PACKAGES waits for the five-tip threshold.
        var vm = Create(todos: Board(new DriverState(0.0, 0.0, CreditEarned: 500.0)));

        Assert.True(vm.HasCompletedTodos);
        string only = Assert.Single(vm.CompletedTodoLines);
        Assert.StartsWith("1. ", only, StringComparison.Ordinal);
        Assert.Contains("Earn 500 CR", only, StringComparison.Ordinal);
        Assert.False(vm.ShowDeliverCarePackages);
        Assert.DoesNotContain("DELIVER CARE PACKAGES", vm.VoiceActions.Keys);
    }

    [Fact]
    public void CarePackages_DeliverOffersItselfAtFiveAndForwardsTheCommand()
    {
        // Six subjects already met and a shop in town: the boot sync grants one
        // kit per completion, so the bag threshold is satisfied.
        TodoBoard board = CareBoard();
        Assert.True(board.CarePackageCount >= 5);
        int delivered = 0;
        var vm = Create(
            todos: board, canDeliverCarePackages: true,
            deliverCarePackages: new RelayCommand(() => delivered++));

        Assert.True(vm.ShowDeliverCarePackages);
        Assert.Contains("6", vm.CarePackageCountLine, StringComparison.Ordinal);
        Assert.Equal(6, vm.CompletedTodoLines.Count);

        // Numbered 1..6: the ordinal is the page's marker (resx
        // TodoCompletedLine), not part of the tip's own copy.
        for (int i = 0; i < vm.CompletedTodoLines.Count; i++)
        {
            Assert.StartsWith($"{i + 1}. ", vm.CompletedTodoLines[i], StringComparison.Ordinal);
        }
        Assert.True(vm.VoiceActions.ContainsKey("DELIVER CARE PACKAGES"));

        // The page forwards the host's command; clearing the tip block and
        // starting the run stay the navigator's composition.
        vm.VoiceActions["DELIVER CARE PACKAGES"].Invoke();
        Assert.Equal(1, delivered);
    }

    [Fact]
    public void CarePackages_DeliverHiddenWhileTheRunOwnsTheCompanion()
    {
        var vm = Create(todos: CareBoard(), canDeliverCarePackages: false);

        Assert.False(vm.ShowDeliverCarePackages);
        Assert.Equal(string.Empty, vm.CarePackageCountLine);
        Assert.DoesNotContain("DELIVER CARE PACKAGES", vm.VoiceActions.Keys);
    }

    [Fact]
    public void Voice_IncludesAllButtons()
    {
        var vm = Create(deliveryOpen: false);
        Assert.True(vm.VoiceActions.ContainsKey("START TAXI SHIFT"));
        Assert.True(vm.VoiceActions.ContainsKey("DELIVERY JOB"));
        Assert.True(vm.VoiceActions.ContainsKey("CAR HAULING JOB"));
        Assert.True(vm.VoiceActions.ContainsKey("POLICE"));
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
            beat, clock, new GameClock(clock, clock.UtcNow), DefaultTodoBoard(),
            new TaxiShift(),
            new ControlActions(
                new RelayCommand(() => { }),
                new RelayCommand(() => fired = true),
                new RelayCommand(() => { }),
                new RelayCommand(() => { }),
                () => false,
                new RelayCommand(() => { }),
                () => RoadServiceOffer.Hidden),
            new RelayCommand(() => { }),
            1);

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

    // The police entry is a placeholder too (PORTPLAN 41's flow has not
    // landed): a tap is real — the button is clickable — but the only result
    // is its own text swapping for COMING SOON and coming back after three
    // real seconds on the injected clock.
    [Fact]
    public void PoliceClick_SaysComingSoonForThreeSecondsThenRestores()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        Assert.Equal("POLICE", vm.PoliceLabel);

        vm.PoliceCommand.Execute(null);
        Assert.Equal("COMING SOON", vm.PoliceLabel);

        clock.Advance(TimeSpan.FromMilliseconds(2999));
        vm.UpdateFlash();
        Assert.Equal("COMING SOON", vm.PoliceLabel); // still inside the hold

        clock.Advance(TimeSpan.FromMilliseconds(2));
        vm.UpdateFlash();
        Assert.Equal("POLICE", vm.PoliceLabel);
    }

    [Fact]
    public void PoliceClick_Again_RestartsTheHold()
    {
        var clock = new FakeClock();
        var vm = Create(clock);

        vm.PoliceCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(2));
        vm.PoliceCommand.Execute(null); // latest tap wins
        clock.Advance(TimeSpan.FromSeconds(2));
        vm.UpdateFlash();
        Assert.Equal("COMING SOON", vm.PoliceLabel); // two seconds into the second hold

        clock.Advance(TimeSpan.FromSeconds(1));
        vm.UpdateFlash();
        Assert.Equal("POLICE", vm.PoliceLabel);
    }

    // Vosk reaches the same command as a click: a spoken "police" glows the
    // button teal (the voice acknowledgment), says COMING SOON, and the text
    // outlives the half-second glow.
    [Theory]
    [InlineData("police")]
    [InlineData("the police")]
    public void Voice_PoliceTap_GlowsAndSaysComingSoon(string spoken)
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        string? label = VoiceMatch.MatchLabel(spoken, vm.VoiceActions.Keys);
        Assert.NotNull(label);
        Assert.Equal("POLICE", label);

        vm.VoiceActions[label].Invoke();
        Assert.True(vm.PoliceFlash);
        Assert.Equal("COMING SOON", vm.PoliceLabel);

        clock.Advance(TimeSpan.FromSeconds(0.51));
        vm.UpdateFlash();
        Assert.False(vm.PoliceFlash);
        Assert.Equal("COMING SOON", vm.PoliceLabel); // the text outlives the glow

        clock.Advance(TimeSpan.FromSeconds(2.5));
        vm.UpdateFlash();
        Assert.Equal("POLICE", vm.PoliceLabel);
    }

    [Fact]
    public void PoliceBack_RestoresTheTextOnTheWayOut()
    {
        // The page is cached, so a player who backs out mid-hold must not find
        // a stale COMING SOON when they come back.
        var vm = Create();
        vm.PoliceCommand.Execute(null);
        Assert.Equal("COMING SOON", vm.PoliceLabel);

        vm.BackCommand.Execute(null);
        Assert.Equal("POLICE", vm.PoliceLabel);
    }

    // The haul's own three-second hold: a refused tap names the reason the flow
    // gave it on the button the player tapped, and the button is its own name
    // again before the next tap.
    [Fact]
    public void HaulGate_SaysTheLoyaltyFloorForThreeSecondsThenRestores()
    {
        var clock = new FakeClock();
        var vm = Create(clock);
        Assert.Equal("CAR HAULING JOB", vm.HaulLabel);

        vm.ShowHaulGate(LoyaltyGate);
        Assert.Equal("NOT ENOUGH BRAND LOYALTY YET", vm.HaulLabel);

        clock.Advance(TimeSpan.FromMilliseconds(2999));
        vm.UpdateFlash();
        Assert.Equal("NOT ENOUGH BRAND LOYALTY YET", vm.HaulLabel); // still inside the hold

        clock.Advance(TimeSpan.FromMilliseconds(2));
        vm.UpdateFlash();
        Assert.Equal("CAR HAULING JOB", vm.HaulLabel);
    }

    [Fact]
    public void HaulGate_Again_RestartsTheHold()
    {
        var clock = new FakeClock();
        var vm = Create(clock);

        vm.ShowHaulGate(LoyaltyGate);
        clock.Advance(TimeSpan.FromSeconds(2));
        vm.ShowHaulGate(LoyaltyGate); // latest tap wins
        clock.Advance(TimeSpan.FromSeconds(2));
        vm.UpdateFlash();
        Assert.Equal("NOT ENOUGH BRAND LOYALTY YET", vm.HaulLabel);

        clock.Advance(TimeSpan.FromSeconds(1));
        vm.UpdateFlash();
        Assert.Equal("CAR HAULING JOB", vm.HaulLabel);
    }

    [Fact]
    public void HaulGate_KeepsTheVoiceDoorThroughARebuild()
    {
        // A refresh can rebuild the voice table inside the hold; the door must
        // stay keyed off the button's own name, never the gate text.
        var clock = new FakeClock();
        var shift = new TaxiShift();
        var vm = Create(clock, shift: shift);

        vm.ShowHaulGate(LoyaltyGate);
        shift.Start(clock.UtcNow);
        vm.Refresh();

        Assert.Equal("NOT ENOUGH BRAND LOYALTY YET", vm.HaulLabel);
        Assert.Contains("CAR HAULING JOB", vm.VoiceActions.Keys);
        Assert.DoesNotContain("NOT ENOUGH BRAND LOYALTY YET", vm.VoiceActions.Keys);
    }

    [Fact]
    public void HaulGateBack_RestoresTheNameOnTheWayOut()
    {
        var vm = Create();
        vm.ShowHaulGate(LoyaltyGate);
        Assert.Equal("NOT ENOUGH BRAND LOYALTY YET", vm.HaulLabel);

        vm.BackCommand.Execute(null);
        Assert.Equal("CAR HAULING JOB", vm.HaulLabel);
    }

    [Fact]
    public void HaulGate_PrintsTheWordsTheFlowChose()
    {
        // The page owns the hold, not the wording: the generic refusal must
        // reach the button exactly like the loyalty one.
        var vm = Create();

        vm.ShowHaulGate("NO CAR HAULING JOB AVAILABLE YET");

        Assert.Equal("NO CAR HAULING JOB AVAILABLE YET", vm.HaulLabel);
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
