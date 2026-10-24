using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Delivery career page runs headless against a fixture catalog:
/// stats, ordered store rows, cart phase flips, career gating and voice
/// actions all resolve from the resx and injected commands (LSP in action).</summary>
public sealed class DeliveryScreenTests
{
    // Adenau Post: 12/40 = 30 % amber (low band: -1/hr), needs 28 -> 2.50*0.7*28 = 49.00
    private static readonly DeliveryStore Post =
        new("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50);

    // Nurburg Kiosk: 6/40 = 15 % red/urgent (empty band: -2/hr), needs 34 -> 2.20*0.7*34 = 52.36
    private static readonly DeliveryStore Kiosk =
        new("nurburg-kiosk", "Nurburg Kiosk", "Fuchs", 6, 40, 2.20);

    // Full Market: 40/40 = 100 % (high band: +2/hr), so it takes no order.
    private static readonly DeliveryStore FullMarket =
        new("full-market", "Full Market", "Schmitz", 40, 40, 4.40);

    private static readonly DeliveryCatalog Catalog =
        new([Post, Kiosk, FullMarket], 0.7, 100);

    private static readonly HubCatalog GrocersAndSonsHubs = new([
        new HubTier(1, true, [
            new HubCareer("groceries", "GROCERIES", [new HubLevel("Grocers and Sons", 100)])]),
    ]);

    private static (
        DeliveryJobScreenViewModel Vm,
        TestLog Log,
        bool[] Fired, // [0]=back [1]=run [2]=hub
        DeliveryCareer Career,
        List<DeliveryRun> Runs)
        Create(DeliveryCatalog? catalog = null, HubCatalog? hubs = null,
            FakeClock? clock = null, VoiceBeat? beat = null,
            double? startCredP = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var log = new TestLog();
        var fired = new bool[3];
        List<DeliveryRun> runs = [];
        var board = catalog ?? Catalog;
        var career = new DeliveryCareer(hubs ?? HubCatalog.Default, startCredP ?? board.StartCredP);
        var city = new CityEconomy(board.Stores, CityRules.Default);
        // Tests that assert the deferred voice navigation own the clock and
        // the beat, so they can advance past the beat and drain it.
        clock ??= new FakeClock();
        beat ??= new VoiceBeat(clock);
        var vm = new DeliveryJobScreenViewModel(
            board, career, city, log,
            new RelayCommand(() => fired[0] = true),
            new ParameterizedCommand<DeliveryRun>(r => { fired[1] = true; runs.Add(r!); }),
            new RelayCommand(() => fired[2] = true),
            beat);
        return (vm, log, fired, career, runs);
    }

    private static DeliveryRow Row(DeliveryJobScreenViewModel vm, string key) =>
        Assert.Single(vm.Rows, r => r.Store.Key == key);

    [Fact]
    public void Boot_ShowsStatsAndOrderedRows()
    {
        var (vm, _, _, _, _) = Create();
        Assert.Equal("Credibility Points: 100", vm.CredPLine);
        // Town math (PORTPLAN 25): -1 + -2 + +2 = -1/hr; three stores seeded
        // at the rules' 100 citizens each.
        Assert.Equal("City growth: ▼ -1 / hr", vm.CityGrowthLine);
        Assert.Equal("Current citizens: 300", vm.CitizensLine);

        // Unconfirmed rows sort by stock percent ascending.
        Assert.Equal(["nurburg-kiosk", "adenau-post", "full-market"],
            vm.Rows.Select(r => r.Store.Key).ToArray());
        Assert.Equal("OPEN NEW CAREER", vm.MainButtonLabel);
        Assert.Equal("BACK", vm.BackButtonLabel);
        Assert.True(vm.HasStores);
    }

    [Fact]
    public void Rows_BandsUrgentAndGrowth_FormattedFromData()
    {
        var (vm, _, _, _, _) = Create();
        DeliveryRow kiosk = Row(vm, "nurburg-kiosk");
        Assert.Equal("15%", kiosk.PercentLine);
        Assert.Equal(StockLevel.Red, kiosk.Level);
        Assert.True(kiosk.IsUrgent);
        Assert.Equal("▼ -2 citizen / hr", kiosk.GrowthLine);
        Assert.Equal("6 / 40 units", kiosk.UnitsLine);
        Assert.Equal("FUCHS", kiosk.OwnerLine);

        DeliveryRow post = Row(vm, "adenau-post");
        Assert.Equal("30%", post.PercentLine);
        Assert.Equal(StockLevel.Amber, post.Level);
        Assert.False(post.IsUrgent);
        Assert.Equal("▼ -1 citizen / hr", post.GrowthLine);

        DeliveryRow full = Row(vm, "full-market");
        Assert.Equal(StockLevel.Green, full.Level);
        Assert.Equal("▲ +2 citizen / hr", full.GrowthLine);
    }

    [Fact]
    public void TapStore_ConfirmsFullRestockOrder()
    {
        var (vm, log, _, _, _) = Create();
        vm.ToggleOrderCommand.Execute(Row(vm, "adenau-post"));

        // Confirmed rows move to the top (cart order), like Unity's staged list.
        DeliveryRow post = Row(vm, "adenau-post");
        Assert.True(post.IsConfirmed);
        Assert.Equal("adenau-post", vm.Rows[0].Store.Key);
        Assert.Equal("Order Confirmed for €49.00", post.ConfirmedLine);
        Assert.Equal(49.00, vm.CartTotal);
        Assert.Equal("DELIVER", vm.MainButtonLabel);
        Assert.Equal("CANCEL ORDER", vm.BackButtonLabel);
        Assert.Contains("order confirmed: Adenau Post", log.Infos[0], StringComparison.Ordinal);
    }

    [Fact]
    public void TapConfirmedRow_DropsTheOrder()
    {
        var (vm, log, _, _, _) = Create();
        var post = Row(vm, "adenau-post");
        vm.ToggleOrderCommand.Execute(post);
        vm.ToggleOrderCommand.Execute(Row(vm, "adenau-post"));

        Assert.False(Row(vm, "adenau-post").IsConfirmed);
        Assert.Equal(0.0, vm.CartTotal);
        Assert.Equal("OPEN NEW CAREER", vm.MainButtonLabel);
        Assert.Contains("order dropped: Adenau Post", string.Join("|", log.Infos), StringComparison.Ordinal);
    }

    [Fact]
    public void TapFullyStocked_IgnoredWithLog()
    {
        var (vm, log, _, _, _) = Create();
        vm.ToggleOrderCommand.Execute(Row(vm, "full-market"));

        Assert.False(Row(vm, "full-market").IsConfirmed);
        Assert.Equal(0.0, vm.CartTotal);
        Assert.Contains("fully stocked: Full Market", string.Join("|", log.Infos), StringComparison.Ordinal);
    }

    [Fact]
    public void UrgentFlag_HidesOnceConfirmed()
    {
        var (vm, _, _, _, _) = Create();
        vm.ToggleOrderCommand.Execute(Row(vm, "nurburg-kiosk"));
        Assert.False(Row(vm, "nurburg-kiosk").IsUrgent);
    }

    [Fact]
    public void MainButton_RoutesByPhase()
    {
        var (vm, _, fired, _, _) = Create();
        vm.MainButtonCommand.Execute(null);
        Assert.True(fired[2]); // empty cart: OPEN NEW CAREER -> hub seam
        Assert.False(fired[1]);

        vm.ToggleOrderCommand.Execute(Row(vm, "adenau-post"));
        vm.MainButtonCommand.Execute(null);
        Assert.True(fired[1]); // orders staged: DELIVER -> run seam
    }

    [Fact]
    public void Deliver_HandsTheRunOffAndEmptiesTheCart()
    {
        var (vm, log, fired, _, runs) = Create();
        vm.ToggleOrderCommand.Execute(Row(vm, "adenau-post"));
        vm.MainButtonCommand.Execute(null);

        // The tap only starts the run: the host owns the drive and fills each
        // shelf at the stop, so the board must not change yet.
        Assert.True(fired[1]);
        // The run carries the job's money exactly as the cart staged it:
        // wholesale out (49.00), retail payout of the live pre-restock
        // shelves in (2.50 × 0.9 × 28 = 63.00).
        DeliveryRun run = Assert.Single(runs);
        Assert.Equal(["adenau-post"], run.StoreKeys);
        Assert.Equal(49.00, run.PaidForGoods);
        Assert.Equal(63.00, run.ReceivedForGoods);
        DeliveryRow post = Row(vm, "adenau-post");
        Assert.Equal("12 / 40 units", post.UnitsLine);
        Assert.Equal("30%", post.PercentLine);
        Assert.Equal("▼ -1 citizen / hr", post.GrowthLine);
        Assert.Equal("6 / 40 units", Row(vm, "nurburg-kiosk").UnitsLine);

        // The cart became a live run, so the page flips back to its intro copy
        // instead of holding an order nothing will pick up.
        Assert.Equal(0.0, vm.CartTotal);
        Assert.Equal("OPEN NEW CAREER", vm.MainButtonLabel);
        Assert.Equal("BACK", vm.BackButtonLabel);
        Assert.Contains("delivery run started: €49.00 -> Adenau Post",
            string.Join("|", log.Infos), StringComparison.Ordinal);
    }

    [Fact]
    public void Deliver_LeavesTheTownAloneUntilTheStopsAreServed()
    {
        // The town's growth is read off the shelves, so starting the run
        // changes nothing: the kiosk is still empty and the header still
        // reads -1/hr until the host serves that stop.
        var (vm, _, _, _, _) = Create();
        Assert.Equal("City growth: ▼ -1 / hr", vm.CityGrowthLine);

        vm.ToggleOrderCommand.Execute(Row(vm, "nurburg-kiosk"));
        vm.MainButtonCommand.Execute(null);

        Assert.Equal("City growth: ▼ -1 / hr", vm.CityGrowthLine);
        Assert.Equal("Current citizens: 300", vm.CitizensLine);
    }

    [Fact]
    public void Back_WithPendingCart_CancelsOrdersFirst()
    {
        var (vm, log, fired, _, _) = Create();
        vm.ToggleOrderCommand.Execute(Row(vm, "nurburg-kiosk"));
        vm.BackCommand.Execute(null);

        Assert.True(fired[0]);
        Assert.Equal(0.0, vm.CartTotal);
        Assert.Equal("OPEN NEW CAREER", vm.MainButtonLabel);
        Assert.Contains("orders cancelled: €52.36 refunded", string.Join("|", log.Infos), StringComparison.Ordinal);
    }

    [Fact]
    public void Back_EmptyCart_OnlyNavigates()
    {
        var (vm, log, fired, _, _) = Create();
        vm.BackCommand.Execute(null);
        Assert.True(fired[0]);
        Assert.Empty(log.Infos);
    }

    [Fact]
    public void EmptyCatalog_ShowsEmptyState()
    {
        var (vm, _, _, _, _) = Create(DeliveryCatalog.Default);
        Assert.False(vm.HasStores);
        Assert.Empty(vm.Rows);
        Assert.Equal("Credibility Points: 100", vm.CredPLine);
        Assert.Equal("City growth: ▬ 0 / hr", vm.CityGrowthLine);
    }

    [Fact]
    public void FirstCredPHint_ShowsOnlyForAFreshCareer()
    {
        // No CredP and no hub: OPEN NEW CAREER cannot buy anything, so the
        // board points at Max's intro gift (App seeds exactly this state).
        var (vm, _, _, career, _) = Create(startCredP: 0);
        Assert.True(vm.ShowFirstCredPHint);

        career.AddCredP(100); // Max's first store visit
        vm.Refresh();
        Assert.False(vm.ShowFirstCredPHint);
    }

    [Fact]
    public void FirstCredPHint_StaysHiddenOnceACareerIsOpen()
    {
        var (vm, _, _, career, _) = Create(catalog: Catalog, hubs: GrocersAndSonsHubs);
        Assert.False(vm.ShowFirstCredPHint);

        Assert.True(career.TryOpenCareer(tierId: 1, careerIndex: 0, levelIndex: 0));
        vm.Refresh(); // Unity re-shows the board after the hub page closes.

        Assert.Equal("Credibility Points: 0", vm.CredPLine);
        Assert.False(vm.ShowFirstCredPHint);
    }

    [Fact]
    public void Stores_ConnectWhenTheirCareerOpens()
    {
        var board = new DeliveryCatalog(
            [Post with { Category = "groceries" }, Kiosk with { Category = "coffee_beans" }], 0.7, 100);
        var (vm, _, _, career, _) = Create(board, GrocersAndSonsHubs);

        // Unity's career gate: an empty board until a hub is bought.
        Assert.False(vm.HasStores);
        Assert.Empty(vm.Rows);
        Assert.Equal("OPEN NEW CAREER", vm.MainButtonLabel);

        Assert.True(career.TryOpenCareer(tierId: 1, careerIndex: 0, levelIndex: 0));
        vm.Refresh(); // Unity re-shows the board after the hub page closes.

        Assert.True(vm.HasStores);
        Assert.Equal(["adenau-post"], vm.Rows.Select(r => r.Store.Key).ToArray());
        Assert.Equal("Credibility Points: 0", vm.CredPLine);
    }

    [Fact]
    public void Voice_BothPhaseNamesAndStoresAnswer()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var (vm, _, fired, _, _) = Create(clock: clock, beat: beat);
        Assert.True(vm.VoiceActions.ContainsKey("OPEN NEW CAREER"));
        Assert.True(vm.VoiceActions.ContainsKey("DELIVER")); // staged even while OPEN shows
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));
        Assert.True(vm.VoiceActions.ContainsKey("CANCEL ORDER"));
        Assert.True(vm.VoiceActions.ContainsKey("cancel"));
        Assert.True(vm.VoiceActions.ContainsKey("Adenau Post"));

        // One button routes by the page's phase, exactly like a click: with
        // an empty cart either name opens the hub seam, not the run. Voice
        // taps wait out the shared beat so the glow shows before the flip.
        vm.VoiceActions["DELIVER"].Invoke();
        DrainBeat(clock, beat);
        Assert.True(fired[2]);
        Assert.False(fired[1]);

        vm.VoiceActions["Nurburg Kiosk"].Invoke();
        Assert.True(Row(vm, "nurburg-kiosk").IsConfirmed);

        // Orders staged: the same names now route to the run seam.
        vm.VoiceActions["DELIVER"].Invoke();
        DrainBeat(clock, beat);
        Assert.True(fired[1]);
    }

    // What MainViewModel.Poll does at 10 Hz: wall time passes, the beat drains.
    private static void DrainBeat(FakeClock clock, VoiceBeat beat)
    {
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
    }

    [Fact]
    public void Voice_MainTap_GlowsFirst_NavigatesAtTheBeatEnd()
    {
        // The page change waits: the tapped button holds its teal glow for
        // the half-second beat and the seam only opens when the beat drains.
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var (vm, _, fired, _, _) = Create(clock: clock, beat: beat);

        vm.VoiceActions["OPEN NEW CAREER"].Invoke();
        Assert.True(vm.MainFlash);
        Assert.False(fired[2]); // still on this page while the beat runs

        DrainBeat(clock, beat);
        Assert.True(fired[2]);
        Assert.False(vm.MainFlash); // no stale glow to re-show later
    }

    [Fact]
    public void Voice_BackTap_WaitsTheBeat_ThenClearsPendingCart()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var (vm, _, fired, _, _) = Create(clock: clock, beat: beat);
        vm.ToggleOrderCommand.Execute(Row(vm, "nurburg-kiosk"));

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(fired[0]); // CANCEL ORDER lands after the beat
        Assert.True(vm.Rows[0].IsConfirmed);

        DrainBeat(clock, beat);
        Assert.True(fired[0]);
        Assert.Equal(0.0, vm.CartTotal);
    }

    [Fact]
    public void Voice_CancelAlone_ClearsPendingCart()
    {
        // The one-word request: "cancel" fires what prints "CANCEL ORDER"
        // (refund + leave), beside the full label that already answered.
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var (vm, _, fired, _, _) = Create(clock: clock, beat: beat);
        vm.ToggleOrderCommand.Execute(Row(vm, "nurburg-kiosk"));

        vm.VoiceActions["cancel"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(fired[0]); // the beat still runs before the page flips
        Assert.True(vm.Rows[0].IsConfirmed);

        DrainBeat(clock, beat);
        Assert.True(fired[0]);
        Assert.Equal(0.0, vm.CartTotal);
    }

    [Fact]
    public void Click_MainAndBack_NavigateAtOnce_NoBeatWait()
    {
        // The wait exists so the voice glow is seen; a click already lands
        // its action instantly, glow or not.
        var (vm, _, fired, _, _) = Create();
        vm.MainButtonCommand.Execute(null);
        Assert.True(fired[2]);
        vm.BackCommand.Execute(null);
        Assert.True(fired[0]);
    }

    [Fact]
    public void NumberWords_TriggerTheStoreAtThatPosition()
    {
        var (vm, _, _, _, _) = Create();
        // Positions follow the displayed row order (stock ascending here).
        Assert.Equal("ONE", vm.Rows[0].NumberWord);
        Assert.Equal("TWO", vm.Rows[1].NumberWord);
        Assert.Equal("THREE", vm.Rows[2].NumberWord);

        // The word answers as button text does: "two" -> the second row.
        vm.VoiceActions["TWO"].Invoke();
        Assert.True(Row(vm, "adenau-post").IsConfirmed);
    }

    [Fact]
    public void CartMath_TotalSumsWholesaleCosts()
    {
        var (vm, _, _, _, _) = Create();
        vm.ToggleOrderCommand.Execute(Row(vm, "adenau-post"));
        vm.ToggleOrderCommand.Execute(Row(vm, "nurburg-kiosk"));
        // 2.50*0.7*28 + 2.20*0.7*34 = 49.00 + 52.36
        Assert.Equal(101.36, vm.CartTotal);
    }
}
