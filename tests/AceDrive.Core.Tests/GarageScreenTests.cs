using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The garage page (PORTPLAN 30): rows carry the catalogue specs, the
/// current car is marked and not pickable, and a switch is reported to the host
/// that banks it.</summary>
public sealed class GarageScreenTests
{
    private static readonly CarSpec Mx5 = new(
        "ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Passengers: 1, Class: 9,
        Price: 12000.0, TrunkLitres: 150);

    private static readonly CarSpec Junior = new(
        "ks_alfa_romeo_junior", 2024, "Alfa Romeo", "junior", Passengers: 4, Class: 6,
        Price: 47500.0, TrunkLitres: 400);

    private static (GarageScreenViewModel Vm, Garage Garage, List<string> Switches) Create(
        GarageState? saved = null, bool withCars = true)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var garage = new Garage([Mx5, Junior], saved);
        if (withCars && garage.Cars.Count == 0)
        {
            garage.Add(Mx5, week: 1);
            garage.Add(Junior, week: 2);
        }
        List<string> switches = [];
        var vm = new GarageScreenViewModel(
            garage, new RelayCommand(() => { }), switches.Add, new VoiceBeat(new FakeClock()));
        return (vm, garage, switches);
    }

    [Fact]
    public void Rows_NameEveryOwnedCarWithItsSpecs()
    {
        var (vm, _, _) = Create();
        Assert.Equal(2, vm.Rows.Count);
        GarageRow row = vm.Rows[0];
        Assert.Equal("2024 ALFA ROMEO JUNIOR", row.Title);
        Assert.Contains("class 6", row.SpecsLine, StringComparison.Ordinal);
        Assert.Contains("4 seats", row.SpecsLine, StringComparison.Ordinal);
        Assert.Contains("trunk 400 L", row.SpecsLine, StringComparison.Ordinal);
        Assert.Contains("0 km", row.SpecsLine, StringComparison.Ordinal);
        Assert.Equal("CR47500", row.ValueLine);
    }

    [Fact]
    public void CurrentCar_IsMarkedAndNotPickable()
    {
        // Unity GarageScreen: the CURRENT CAR row is non-interactable.
        var (vm, garage, switches) = Create();
        GarageRow current = Assert.Single(vm.Rows, r => r.IsCurrent);
        Assert.Equal(garage.CurrentKey, current.Key);
        Assert.False(current.IsSelectable);
        Assert.All(vm.Rows.Where(r => !r.IsCurrent), r => Assert.True(r.IsSelectable));

        vm.SwitchCommand.Execute(current);
        Assert.Empty(switches);
    }

    [Fact]
    public void Switch_MakesTheTappedCarCurrentAndReportsIt()
    {
        var (vm, garage, switches) = Create();
        GarageRow other = Assert.Single(vm.Rows, r => !r.IsCurrent);
        vm.SwitchCommand.Execute(other);

        Assert.Equal(other.Key, garage.CurrentKey);
        Assert.Single(switches);
        Assert.Equal(other.Key, switches[0]);
        // The page repainted: the marker moved with the switch.
        Assert.Equal(other.Key, Assert.Single(vm.Rows, r => r.IsCurrent).Key);
    }

    [Fact]
    public void EmptyGarage_ShowsTheHint()
    {
        var (vm, _, _) = Create(withCars: false);
        Assert.True(vm.IsEmpty);
        Assert.Empty(vm.Rows);
        Assert.False(string.IsNullOrWhiteSpace(vm.EmptyLine));
    }

    [Fact]
    public void RetiredCar_ShowsItsKeyInsteadOfVanishing()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var garage = new Garage([Mx5], new GarageState(null, [
            new OwnedCarState("ks_removed_car", 1, 5.0, false),
        ]));
        var vm = new GarageScreenViewModel(
            garage, new RelayCommand(() => { }), _ => { }, new VoiceBeat(new FakeClock()));
        GarageRow row = Assert.Single(vm.Rows);
        Assert.Equal("KS_REMOVED_CAR", row.Title);
        Assert.Equal(string.Empty, row.SpecsLine);
        Assert.Equal(string.Empty, row.ValueLine);
    }

    [Fact]
    public void NoScreenWrite_BackCommandIsTheHosts()
    {
        bool backed = false;
        var garage = new Garage([Mx5]);
        var vm = new GarageScreenViewModel(
            garage, new RelayCommand(() => backed = true), _ => { }, new VoiceBeat(new FakeClock()));
        Assert.IsAssignableFrom<ICommand>(vm.BackCommand);
        vm.BackCommand.Execute(null);
        Assert.True(backed);
    }

    [Fact]
    public void Voice_BackGlowsThenNavigatesAfterTheBeat()
    {
        // Back navigates, so its voice tap glows teal first and the page waits
        // out the shared beat before the host's command fires (bank precedent).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool backed = false;
        var vm = new GarageScreenViewModel(
            new Garage([Mx5]), new RelayCommand(() => backed = true), _ => { }, beat);

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(backed); // still on this page while the beat runs

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(backed);
        Assert.False(vm.BackFlash); // no stale glow to re-show later
    }
}
