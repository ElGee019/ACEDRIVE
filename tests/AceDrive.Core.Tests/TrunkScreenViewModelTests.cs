using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The trunk page (Unity YouScreen's TrunkScreen): free space against the
/// current car, the appetite label, one row per carried stack and the EAT tap
/// that consumes a unit and feeds the driver — plus the glove compartment,
/// which carries the non-food keepsakes and offers no bite.
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

    // A Cento Miglia certificate: the game hands it over as a keepsake, so the
    // catalogue has no row for it and it belongs in the glove compartment.
    private const string Certificate = "Cento Miglia Certificate Week 3 1924 - Tier 1";

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

    private static TrunkItemRow CompartmentRow(TrunkScreenViewModel vm, string name) =>
        vm.CompartmentRows.First(row => row.Name == name);

    [Fact]
    public void Open_RendersFreeSpaceHungerAndStacks()
    {
        var (vm, _, _, _) = Create(
            items: [new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 2 }]);

        // Unity: "Free space: {remaining:F2}/{max}L" and "HUNGER {h}/36".
        Assert.Equal("Free space: 59.20/60L", vm.FreeSpaceLine);
        Assert.Equal("HUNGER 0/36", vm.HungerLine);
        Assert.False(vm.HungerAmber);
        Assert.False(vm.IsTrunkEmpty);
        Assert.True(vm.IsCompartmentEmpty);

        TrunkItemRow row = Row(vm, "MAPLE SYRUP");
        Assert.Equal("x2", row.QuantityLine);
        Assert.Equal("-3", row.HungerLine);
        Assert.True(row.CanEat);
    }

    [Fact]
    public void Open_EmptyTrunk_ShowsTheHint()
    {
        var (vm, _, _, _) = Create();

        Assert.True(vm.IsTrunkEmpty);
        Assert.True(vm.IsCompartmentEmpty);
        Assert.Empty(vm.Rows);
        Assert.Empty(vm.CompartmentRows);
        Assert.Equal("Your trunk is empty.", vm.TrunkEmptyLine);
        Assert.Equal("Your glove compartment is empty.", vm.CompartmentEmptyLine);
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
        Assert.True(vm.IsTrunkEmpty);
        Assert.Equal("Free space: 60.00/60L", vm.FreeSpaceLine);
    }

    [Fact]
    public void Open_SplitsFoodFromTheGloveCompartment()
    {
        // The certificate is not a catalogue row: Unity keeps it in the glove
        // compartment, so the trunk lists the maple syrup alone.
        var (vm, _, _, _) = Create(items:
        [
            new TrunkItem("MAPLE SYRUP", 0.4) { Quantity = 2 },
            new TrunkItem(Certificate, 0.1) { Quantity = 1 },
        ]);

        Assert.Equal(["MAPLE SYRUP"], vm.Rows.Select(row => row.Name));
        Assert.Equal([Certificate], vm.CompartmentRows.Select(row => row.Name));

        TrunkItemRow keepsake = Assert.Single(vm.CompartmentRows);
        Assert.Equal("x1", keepsake.QuantityLine);
        Assert.Equal(string.Empty, keepsake.HungerLine);
        Assert.False(keepsake.CanEat);
        Assert.False(vm.IsTrunkEmpty);
        Assert.False(vm.IsCompartmentEmpty);
    }

    [Fact]
    public void Open_OnlyKeepsakes_LeavesTheTrunkEmptyAndTheCompartmentFull()
    {
        var (vm, _, _, _) = Create(
            items: [new TrunkItem(Certificate, 0.1) { Quantity = 1 }]);

        Assert.True(vm.IsTrunkEmpty);
        Assert.False(vm.IsCompartmentEmpty);
        Assert.Empty(vm.Rows);
        Assert.Single(vm.CompartmentRows);
    }

    [Fact]
    public void Open_CatalogueRowThatFeedsNothing_GoesToTheCompartment()
    {
        // The catalogue's hunger value is the food test: a row that would feed
        // nothing is a keepsake, not a meal with its EAT tap.
        var (vm, _, _, _) = Create(
            items: [new TrunkItem("SOUVENIR MUG", 0.5) { Quantity = 1 }],
            catalog: [new StoreItem("SOUVENIR MUG", 6.0, 0.5, 0, "groceries")]);

        Assert.Empty(vm.Rows);
        TrunkItemRow mug = Assert.Single(vm.CompartmentRows);
        Assert.Equal(string.Empty, mug.HungerLine);
        Assert.False(mug.CanEat);
    }

    [Fact]
    public void Eat_CompartmentRow_KeepsTheStackAndTheMeter()
    {
        // Nothing in the compartment can be eaten: a stray tap (or a retired
        // catalogue row) must not feed the driver an invented value.
        var (vm, trunk, hunger, events) = Create(
            hunger: 10,
            items: [new TrunkItem(Certificate, 0.1) { Quantity = 1 }]);

        vm.EatCommand.Execute(Assert.Single(vm.CompartmentRows));

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
