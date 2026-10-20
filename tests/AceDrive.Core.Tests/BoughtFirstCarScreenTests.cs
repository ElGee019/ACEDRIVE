using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The post-purchase choice (PORTPLAN 29, Unity BoughtFirstCar): the
/// car bought, the two ways out and Unity's second-car summary gate.</summary>
public sealed class BoughtFirstCarScreenTests
{
    private static readonly CarSpec Mx5 = new(
        "ks_mazda_mx5_na", 1990, "Mazda", "mx5 na", Price: 12000.0);

    private static (BoughtFirstCarScreenViewModel Vm, List<string> Clicks) Create(bool secondCar)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        List<string> clicks = [];
        var vm = new BoughtFirstCarScreenViewModel(
            Mx5, secondCar,
            new RelayCommand(() => clicks.Add("drive")),
            new RelayCommand(() => clicks.Add("keep")));
        return (vm, clicks);
    }

    [Fact]
    public void NamesTheBoughtCar()
    {
        var (vm, _) = Create(secondCar: false);
        Assert.Equal("1990 MAZDA MX5 NA", vm.CarLine);
        Assert.False(string.IsNullOrWhiteSpace(vm.Title));
        Assert.False(string.IsNullOrWhiteSpace(vm.Body));
    }

    [Fact]
    public void FirstPurchase_HidesTheSummary()
    {
        // Unity shows the summary block only when the garage now holds two
        // cars (grandma's starter plus this purchase).
        var (vm, _) = Create(secondCar: false);
        Assert.False(vm.HasSummary);
        Assert.Equal(string.Empty, vm.SummaryLine);
    }

    [Fact]
    public void SecondCar_ShowsTheSummary()
    {
        var (vm, _) = Create(secondCar: true);
        Assert.True(vm.HasSummary);
        Assert.False(string.IsNullOrWhiteSpace(vm.SummaryLine));
    }

    [Fact]
    public void BothButtons_ReportToTheHost()
    {
        var (vm, clicks) = Create(secondCar: true);
        Assert.IsAssignableFrom<ICommand>(vm.DriveCommand);
        vm.DriveCommand.Execute(null);
        vm.KeepCommand.Execute(null);
        Assert.Equal(["drive", "keep"], clicks);
    }
}
