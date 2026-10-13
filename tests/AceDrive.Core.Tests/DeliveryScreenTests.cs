using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Delivery career page runs headless against a fixture catalog:
/// stats, ordered store rows, cart phase flips and voice actions all resolve
/// from the resx and injected commands (LSP in action).</summary>
public sealed class DeliveryScreenTests
{
    // Adenau Post: 12/40 = 30 % amber, needs 28 -> wholesale 2.50*0.7*28 = 49.00
    private static readonly DeliveryStore Post =
        new("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, 140, 1);

    // Nurburg Kiosk: 6/40 = 15 % red/urgent, needs 34 -> 2.20*0.7*34 = 52.36
    private static readonly DeliveryStore Kiosk =
        new("nurburg-kiosk", "Nurburg Kiosk", "Fuchs", 6, 40, 2.20, 45, -2);

    private static readonly DeliveryStore FullMarket =
        new("full-market", "Full Market", "Schmitz", 40, 40, 4.40, 70, 0);

    private static readonly DeliveryCatalog Catalog =
        new([Post, Kiosk, FullMarket], 0.7, 100);

    private static (
        DeliveryJobScreenViewModel Vm,
        TestLog Log,
        bool[] Fired) // [0]=back [1]=run [2]=hub
        Create(DeliveryCatalog? catalog = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var log = new TestLog();
        var fired = new bool[3];
        var vm = new DeliveryJobScreenViewModel(
            catalog ?? Catalog, log,
            new RelayCommand(() => fired[0] = true),
            new RelayCommand(() => fired[1] = true),
            new RelayCommand(() => fired[2] = true));
        return (vm, log, fired);
    }

    private static DeliveryRow Row(DeliveryJobScreenViewModel vm, string key) =>
        Assert.Single(vm.Rows, r => r.Store.Key == key);

    [Fact]
    public void Boot_ShowsStatsAndOrderedRows()
    {
        var (vm, _, _) = Create();
        Assert.Equal("Credibility Points: 100", vm.CredPLine);
        Assert.Equal("City growth: ▼ -1 / hr", vm.CityGrowthLine);
        Assert.Equal("Current citizens: 255", vm.CitizensLine);

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
        var (vm, _, _) = Create();
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
        Assert.Equal("▲ +1 citizen / hr", post.GrowthLine);

        DeliveryRow full = Row(vm, "full-market");
        Assert.Equal(StockLevel.Green, full.Level);
        Assert.Equal("▬ 0 citizen / hr", full.GrowthLine);
    }

    [Fact]
    public void TapStore_ConfirmsFullRestockOrder()
    {
        var (vm, log, _) = Create();
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
        var (vm, log, _) = Create();
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
        var (vm, log, _) = Create();
        vm.ToggleOrderCommand.Execute(Row(vm, "full-market"));

        Assert.False(Row(vm, "full-market").IsConfirmed);
        Assert.Equal(0.0, vm.CartTotal);
        Assert.Contains("fully stocked: Full Market", string.Join("|", log.Infos), StringComparison.Ordinal);
    }

    [Fact]
    public void UrgentFlag_HidesOnceConfirmed()
    {
        var (vm, _, _) = Create();
        vm.ToggleOrderCommand.Execute(Row(vm, "nurburg-kiosk"));
        Assert.False(Row(vm, "nurburg-kiosk").IsUrgent);
    }

    [Fact]
    public void MainButton_RoutesByPhase()
    {
        var (vm, _, fired) = Create();
        vm.MainButtonCommand.Execute(null);
        Assert.True(fired[2]); // empty cart: OPEN NEW CAREER -> hub seam
        Assert.False(fired[1]);

        vm.ToggleOrderCommand.Execute(Row(vm, "adenau-post"));
        vm.MainButtonCommand.Execute(null);
        Assert.True(fired[1]); // orders staged: DELIVER -> run seam
    }

    [Fact]
    public void Back_WithPendingCart_CancelsOrdersFirst()
    {
        var (vm, log, fired) = Create();
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
        var (vm, log, fired) = Create();
        vm.BackCommand.Execute(null);
        Assert.True(fired[0]);
        Assert.Empty(log.Infos);
    }

    [Fact]
    public void EmptyCatalog_ShowsEmptyState()
    {
        var (vm, _, _) = Create(DeliveryCatalog.Default);
        Assert.False(vm.HasStores);
        Assert.Empty(vm.Rows);
        Assert.Equal("Credibility Points: 100", vm.CredPLine);
        Assert.Equal("City growth: ▬ 0 / hr", vm.CityGrowthLine);
    }

    [Fact]
    public void Voice_BothPhaseNamesAndStoresAnswer()
    {
        var (vm, _, fired) = Create();
        Assert.True(vm.VoiceActions.ContainsKey("OPEN NEW CAREER"));
        Assert.True(vm.VoiceActions.ContainsKey("DELIVER")); // staged even while OPEN shows
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));
        Assert.True(vm.VoiceActions.ContainsKey("CANCEL ORDER"));
        Assert.True(vm.VoiceActions.ContainsKey("Adenau Post"));

        // One button routes by the page's phase, exactly like a click: with
        // an empty cart either name opens the hub seam, not the run.
        vm.VoiceActions["DELIVER"].Invoke();
        Assert.True(fired[2]);
        Assert.False(fired[1]);

        vm.VoiceActions["Nurburg Kiosk"].Invoke();
        Assert.True(Row(vm, "nurburg-kiosk").IsConfirmed);

        // Orders staged: the same names now route to the run seam.
        vm.VoiceActions["DELIVER"].Invoke();
        Assert.True(fired[1]);
    }

    [Fact]
    public void CartMath_TotalSumsWholesaleCosts()
    {
        var (vm, _, _) = Create();
        vm.ToggleOrderCommand.Execute(Row(vm, "adenau-post"));
        vm.ToggleOrderCommand.Execute(Row(vm, "nurburg-kiosk"));
        // 2.50*0.7*28 + 2.20*0.7*34 = 49.00 + 52.36
        Assert.Equal(101.36, vm.CartTotal);
    }
}
