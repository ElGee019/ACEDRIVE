using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The garage page (PORTPLAN 30): the owned cars in a picker, the
/// picked car's Unity dealer-style stats, the current car marked and not
/// switchable, and a switch reported to the host that banks it.</summary>
public sealed class GarageScreenTests
{
    private static readonly CarSpec Mx5 = new(
        "ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Passengers: 1, Class: 9,
        Price: 12000.0, TrunkLitres: 150, Horsepower: 139, EngineCc: 1598,
        FuelPer100Km: 8.0);

    private static readonly CarSpec Junior = new(
        "ks_alfa_romeo_junior", 2024, "Alfa Romeo", "junior", Passengers: 4, Class: 6,
        Price: 47500.0, TrunkLitres: 400, Horsepower: 280, EngineCc: 0,
        FuelPer100Km: 0.0);

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
    public void Picker_ListsEveryOwnedCarAndMarksTheCurrentOne()
    {
        var (vm, garage, _) = Create();
        Assert.Equal(2, vm.Cars.Count);
        Assert.Equal("2024 ALFA ROMEO JUNIOR", vm.Cars[0].Title);
        Assert.Equal("1990 MAZDA MX5 NA", vm.Cars[1].Title);

        GarageRow current = Assert.Single(vm.Cars, r => r.IsCurrent);
        Assert.Equal(garage.CurrentKey, current.Key);
        Assert.True(vm.HasCars);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public void CurrentCar_OpensSelectedTheWayUnitySelectsItOnEntry()
    {
        var (vm, garage, _) = Create();
        Assert.NotNull(vm.SelectedCar);
        Assert.Equal(garage.CurrentKey, vm.SelectedCar!.Key);
        Assert.False(vm.SwitchEnabled);
        Assert.Equal("CURRENT CAR", vm.SwitchLabel);
        Assert.NotEmpty(vm.Stats);
    }

    [Fact]
    public void Stats_ShowTheUnityDealerPreviewValues()
    {
        string[] expectedLabels =
            ["PRICE", "ENGINE", "POWER", "CONSUMPTION", "PASSENGERS", "BOOT", "ODOMETER"];
        var (vm, _, _) = Create();

        vm.SelectedCar = Assert.Single(vm.Cars, r => r.Key == Mx5.Key);

        Assert.Equal(expectedLabels, vm.Stats.Select(s => s.Label));
        Assert.Equal("CR12000", vm.Stats[0].Value);
        Assert.Equal("1598 cc", vm.Stats[1].Value);
        Assert.Equal("139 hp", vm.Stats[2].Value);
        Assert.Equal("8 L/100km", vm.Stats[3].Value);
        Assert.Equal("1", vm.Stats[4].Value);
        Assert.Equal("150 L", vm.Stats[5].Value);
        Assert.Equal("0 km", vm.Stats[6].Value);
    }

    [Fact]
    public void Stats_FollowThePickedCar()
    {
        var (vm, _, _) = Create();

        vm.SelectedCar = Assert.Single(vm.Cars, r => r.Key == Junior.Key);
        Assert.Equal("280 hp", vm.Stats[2].Value);
        Assert.Equal("CR47500", vm.Stats[0].Value);
        Assert.True(vm.SwitchEnabled);
        Assert.Equal("DRIVE THIS CAR", vm.SwitchLabel);

        vm.SelectedCar = Assert.Single(vm.Cars, r => r.Key == Mx5.Key);
        Assert.Equal("139 hp", vm.Stats[2].Value);
        Assert.False(vm.SwitchEnabled);
        Assert.Equal("CURRENT CAR", vm.SwitchLabel);
    }

    [Fact]
    public void Stats_CarryTheOwnedCarsOdometer()
    {
        // The port's per-car reading, resumed from the garage save.
        var (vm, _, _) = Create(new GarageState(
            Mx5.Key, [new OwnedCarState(Mx5.Key, 1, 1234.5, false)]));

        Assert.Equal("1235 km", vm.Stats[6].Value);
    }

    [Fact]
    public void Switch_MakesThePickedCarCurrentAndReportsIt()
    {
        var (vm, garage, switches) = Create();
        GarageRow other = Assert.Single(vm.Cars, r => !r.IsCurrent);

        vm.SelectedCar = other;
        vm.SwitchCommand.Execute(null);

        Assert.Equal(other.Key, garage.CurrentKey);
        Assert.Single(switches);
        Assert.Equal(other.Key, switches[0]);
        // The picker repainted: the marker moved, the pick stayed selected.
        Assert.Equal(other.Key, Assert.Single(vm.Cars, r => r.IsCurrent).Key);
        Assert.Equal(other.Key, vm.SelectedCar!.Key);
        Assert.False(vm.SwitchEnabled);
        Assert.Equal("CURRENT CAR", vm.SwitchLabel);
    }

    [Fact]
    public void Switch_OnTheCurrentCarIsANoOp()
    {
        var (vm, garage, switches) = Create();
        string before = garage.CurrentKey!;

        Assert.False(vm.SwitchCommand.CanExecute(null));
        vm.SwitchCommand.Execute(null);

        Assert.Equal(before, garage.CurrentKey);
        Assert.Empty(switches);
    }

    [Fact]
    public void EmptyGarage_ShowsTheHintAndNoForm()
    {
        var (vm, _, _) = Create(withCars: false);
        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasCars);
        Assert.Empty(vm.Cars);
        Assert.Empty(vm.Stats);
        Assert.Null(vm.SelectedCar);
        Assert.False(string.IsNullOrWhiteSpace(vm.EmptyLine));
    }

    [Fact]
    public void RetiredCar_ShowsItsKeyWithNoStats()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var garage = new Garage([Mx5], new GarageState(null, [
            new OwnedCarState("ks_removed_car", 1, 5.0, false),
        ]));
        var vm = new GarageScreenViewModel(
            garage, new RelayCommand(() => { }), _ => { }, new VoiceBeat(new FakeClock()));
        GarageRow row = Assert.Single(vm.Cars);
        Assert.Equal("KS_REMOVED_CAR", row.Title);
        Assert.Empty(vm.Stats);
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
    public void Voice_CarTitlePicksAndDriveLabelSwitches()
    {
        // Both stay on the page, so both act at once like a click; the drive
        // label carries the same action in either state (no fork with the
        // disabled button), and the guard inside the command keeps it honest.
        var (vm, garage, switches) = Create();
        vm.VoiceActions["2024 ALFA ROMEO JUNIOR"].Invoke();
        Assert.Equal(Junior.Key, vm.SelectedCar!.Key);
        Assert.Equal(Mx5.Key, garage.CurrentKey);

        vm.VoiceActions["DRIVE THIS CAR"].Invoke();
        Assert.Equal(Junior.Key, garage.CurrentKey);
        Assert.Single(switches);

        vm.VoiceActions["CURRENT CAR"].Invoke();
        Assert.Single(switches);
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
