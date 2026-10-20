using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The car wash page (PORTPLAN 33, port of Unity CarWashScreen): the
/// car driven now, the flat CR50 price, the single wash move and its result
/// line, with the host owning the bank.</summary>
public sealed class CarWashScreenTests
{
    private static readonly CarSpec Mx5 = new(
        "ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Passengers: 1, Class: 9,
        Price: 12000.0, TrunkLitres: 150);

    private static (CarWashScreenViewModel Vm, Garage Garage, DriverWallet Wallet,
        List<(int Removed, double Paid)> Washes) Create(
        int filth = 0, double cash = 500.0, bool withCar = true)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var garage = new Garage([Mx5]);
        if (withCar)
        {
            garage.Add(Mx5, week: 1).Filth.Add(filth);
        }
        var wallet = new DriverWallet(new DriverState(cash, 0.0));
        List<(int Removed, double Paid)> washes = [];
        var vm = new CarWashScreenViewModel(
            garage, wallet, new VoiceBeat(new FakeClock()),
            new RelayCommand(() => { }),
            (removed, paid) => washes.Add((removed, paid)));
        return (vm, garage, wallet, washes);
    }

    [Fact]
    public void Page_ShowsTheCarReadingAndFlatPrice()
    {
        // Unity CarWashScreen.RefreshUI: the driven car, "Filth: x / 25" and
        // the flat "Price to wash your car".
        var (vm, _, _, _) = Create(filth: 12);
        Assert.Equal("1990 MAZDA MX5 NA", vm.CarLine);
        Assert.Equal("Filth: 12 / 25", vm.FilthLine);
        Assert.Equal("Price to wash your car: CR50.00", vm.PriceLine);
        Assert.False(vm.ResultVisible);
        Assert.True(vm.WashCommand.CanExecute(null));
    }

    [Fact]
    public void Wash_SpendsTheFlatPriceAndClearsTheCar()
    {
        // Unity CarWashService.WashCar: one CR50 move whatever the reading,
        // and the reading clears; the host is told what came off.
        var (vm, garage, wallet, washes) = Create(filth: 7, cash: 500.0);

        vm.WashCommand.Execute(null);

        Assert.Equal(0, garage.Current!.Filth.Value);
        Assert.Equal(450.0, wallet.Cash);
        Assert.False(vm.WashCommand.CanExecute(null)); // one wash per visit
        Assert.True(vm.ResultVisible);
        Assert.Equal("Removed 7 filth for CR 50.00", vm.ResultLine);
        Assert.Equal((7, 50.0), Assert.Single(washes));
    }

    [Fact]
    public void CleanCar_RefusesTheWash()
    {
        var (vm, garage, wallet, washes) = Create(filth: 0);
        Assert.False(vm.WashCommand.CanExecute(null));

        vm.WashCommand.Execute(null);

        Assert.Equal(0, garage.Current!.Filth.Value);
        Assert.Equal(500.0, wallet.Cash);
        Assert.Empty(washes);
    }

    [Fact]
    public void ShortWallet_RefusesTheWash()
    {
        // Unity's cash < WashCost return: the tap legally does nothing.
        var (vm, garage, wallet, washes) = Create(filth: 9, cash: 20.0);
        Assert.False(vm.WashCommand.CanExecute(null));

        vm.WashCommand.Execute(null);

        Assert.Equal(9, garage.Current!.Filth.Value);
        Assert.Equal(20.0, wallet.Cash);
        Assert.Empty(washes);
    }

    [Fact]
    public void EmptyGarage_ShowsTheNoCarLine()
    {
        var (vm, _, _, _) = Create(withCar: false);
        Assert.Equal("No car to wash yet.", vm.CarLine);
        Assert.Equal(string.Empty, vm.FilthLine);
        Assert.False(vm.WashCommand.CanExecute(null));
    }

    [Fact]
    public void BackCommand_IsTheHosts()
    {
        bool backed = false;
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var garage = new Garage([Mx5]);
        var vm = new CarWashScreenViewModel(
            garage, new DriverWallet(), new VoiceBeat(new FakeClock()),
            new RelayCommand(() => backed = true), (_, _) => { });

        Assert.IsAssignableFrom<ICommand>(vm.BackCommand);
        vm.BackCommand.Execute(null);
        Assert.True(backed);
    }

    [Fact]
    public void Voice_AnswersToWashAndBack()
    {
        var (vm, _, _, _) = Create(filth: 3);
        Assert.True(vm.VoiceActions.ContainsKey("WASH IT"));
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));
    }
}
