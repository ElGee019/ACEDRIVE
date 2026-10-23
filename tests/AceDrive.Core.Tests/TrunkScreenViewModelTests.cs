using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The trunk page (Unity YouScreen's TrunkScreen): free space against the
/// current car, the appetite label, one row per carried stack and the EAT tap
/// that consumes a unit and feeds the driver.
/// </summary>
public sealed class TrunkScreenViewModelTests
{
    // The page copy follows the UI culture; tests pin en-US.
    public TrunkScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    private static readonly StoreItem Maple =
        new("MAPLE SYRUP", 45.99, 0.4, 3, "groceries");

    private static readonly StoreItem Banana =
        new("BANANA", 2.0, 0.3, 4, "groceries");

    private static (
        TrunkScreenViewModel Vm, Trunk Trunk, Hunger Hunger, List<string> Events)
        Create(
            int trunkLitres = 60, int hunger = 0,
            IReadOnlyList<TrunkItem>? items = null,
            IReadOnlyList<StoreItem>? catalog = null)
    {
        var trunk = new Trunk(items ?? []);
        var appetite = new Hunger(
            HungerRules.Default, new HungerState(hunger, ThresholdTriggered: false));
        var car = new CarSpec(
            "ks_test_grocery", 2020, "Test", "Grocery", TrunkLitres: trunkLitres);
        var garage = new Garage([car]);
        garage.Add(car, week: 1);
        var events = new List<string>();
        var vm = new TrunkScreenViewModel(
            trunk, appetite, garage,
            new StoreCatalog(catalog ?? [Maple, Banana], DefaultStock: 20),
            new RelayCommand(() => events.Add("back")),
            () => events.Add("eaten"),
            new VoiceBeat(new FakeClock()));
        return (vm, trunk, appetite, events);
    }

    private static TrunkItemRow Row(TrunkScreenViewModel vm, string name) =>
        vm.Rows.First(row => row.Name == name);

    [Fact]
    public void Open_RendersFreeSpaceHungerAndStacks()
    {
        var (vm, _, _, _) = Create(
            items: [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 2 }]);

        // Unity: "Free space: {remaining:F2}/{max}L" and "HUNGER {h}/36".
        Assert.Equal("Free space: 59.20/60L", vm.FreeSpaceLine);
        Assert.Equal("HUNGER 0/36", vm.HungerLine);
        Assert.False(vm.HungerAmber);
        Assert.False(vm.IsEmpty);

        TrunkItemRow row = Row(vm, "MAPLE SYRUP");
        Assert.Equal("x2", row.QuantityLine);
        Assert.Equal("-3", row.HungerLine);
        Assert.True(row.CanEat);
    }

    [Fact]
    public void Open_EmptyTrunk_ShowsTheHint()
    {
        var (vm, _, _, _) = Create();

        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Rows);
        Assert.Equal("Your trunk is empty.", vm.EmptyLine);
        // No car capacity left over: the whole trunk is free.
        Assert.Equal("Free space: 60.00/60L", vm.FreeSpaceLine);
    }

    [Fact]
    public void HungerAmber_PaintsUnitysBand()
    {
        var (amber, _, _, _) = Create(hunger: HungerRules.AmberFrom);
        var (calm, _, _, _) = Create(hunger: HungerRules.AmberUntil);

        Assert.True(amber.HungerAmber);
        Assert.False(calm.HungerAmber);
        Assert.Equal("HUNGER 16/36", amber.HungerLine);
    }

    [Fact]
    public void Eat_ConsumesOneUnitAndFeedsTheDriver()
    {
        var (vm, trunk, hunger, events) = Create(
            hunger: 10,
            items: [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 2 }]);

        vm.EatCommand.Execute(Row(vm, "MAPLE SYRUP"));

        // One bite: a unit leaves the stack, the catalogue's hunger value
        // leaves the meter, and the host banks the change.
        Assert.Equal(1, trunk.Items.Single().Quantity);
        Assert.Equal("x1", Row(vm, "MAPLE SYRUP").QuantityLine);
        Assert.Equal(7, hunger.Value);
        Assert.Equal("HUNGER 7/36", vm.HungerLine);
        Assert.Equal("Free space: 59.60/60L", vm.FreeSpaceLine);
        Assert.Equal(["eaten"], events);
    }

    [Fact]
    public void Eat_LastUnitDropsTheStack_AndThePageGoesEmpty()
    {
        var (vm, trunk, _, _) = Create(
            items: [new TrunkItem("BANANA", 0.3) { Quantity = 1 }]);

        vm.EatCommand.Execute(Row(vm, "BANANA"));

        Assert.Empty(trunk.Items);
        Assert.True(vm.IsEmpty);
        Assert.Equal("Free space: 60.00/60L", vm.FreeSpaceLine);
    }

    [Fact]
    public void Eat_ItemMissingFromTheCatalogue_KeepsTheStackAndTheMeter()
    {
        // A retired catalogue row must not feed the driver an invented value.
        var (vm, trunk, hunger, events) = Create(
            hunger: 10,
            items: [new TrunkItem("OLD SPECIAL", 0.5) { Quantity = 1 }]);

        TrunkItemRow row = Row(vm, "OLD SPECIAL");
        Assert.False(row.CanEat);
        Assert.Equal(string.Empty, row.HungerLine);

        vm.EatCommand.Execute(row);

        Assert.Equal(1, trunk.Items.Single().Quantity);
        Assert.Equal(10, hunger.Value);
        Assert.Empty(events);
    }

    [Fact]
    public void Eat_EmptyParameter_IsANoOp()
    {
        var (vm, trunk, _, events) = Create(
            items: [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 1 }]);

        vm.EatCommand.Execute(null);

        Assert.Equal(1, trunk.Items.Single().Quantity);
        Assert.Empty(events);
    }

    [Fact]
    public void Back_Click_NavigatesAtOnce()
    {
        var (vm, _, _, events) = Create();

        vm.BackCommand.Execute(null);

        Assert.Equal(["back"], events);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void VoiceBack_GlowsThenNavigates()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var events = new List<string>();
        var vm = new TrunkScreenViewModel(
            new Trunk(), new Hunger(HungerRules.Default), new Garage([]),
            StoreCatalog.Default, new RelayCommand(() => events.Add("back")),
            () => { }, beat);

        Assert.True(vm.VoiceActions.ContainsKey("BACK"));
        vm.VoiceActions["BACK"].Invoke();

        Assert.True(vm.BackFlash);
        Assert.Empty(events);

        // What the 10 Hz poll does: wall time passes and the beat drains.
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.Equal(["back"], events);
        Assert.False(vm.BackFlash);
    }
}
