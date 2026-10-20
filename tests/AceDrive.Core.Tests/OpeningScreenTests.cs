using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Opening screen (Career Start Screen 1) form behaviour.</summary>
public sealed class OpeningScreenTests
{
    private static (OpeningScreenViewModel Vm, List<PlayerProfile> Confirmed) Create(
        PlayerProfile? saved = null,
        string? detected = "ElGee",
        AddressMap? map = null)
    {
        var confirmed = new List<PlayerProfile>();
        var vm = new OpeningScreenViewModel(
            map ?? AddressMap.Default, saved, detected, confirmed.Add);
        return (vm, confirmed);
    }

    [Fact]
    public void SavedProfile_PrefillsEveryField()
    {
        var saved = new PlayerProfile("Edited Name", "Germany", "Brands", "street-2", 17);
        var (vm, _) = Create(saved: saved);

        Assert.Equal("Edited Name", vm.DisplayName);
        Assert.Equal("Germany", vm.Country);
        Assert.Equal("Brands", vm.City);
        Assert.Equal("street-2", vm.SelectedStreet!.Id);
        Assert.Equal(17, vm.SelectedHouseNumber);
        Assert.Equal(33, vm.HouseNumbers.Count);
        Assert.True(vm.CanContinue);
    }

    [Fact]
    public void NoSavedProfile_UsesDetectedNameAndFirstAddress()
    {
        var (vm, _) = Create(detected: "SteamGuy");

        Assert.Equal("SteamGuy", vm.DisplayName);
        Assert.Equal("street-1", vm.SelectedStreet!.Id);
        Assert.Equal(1, vm.SelectedHouseNumber);
        Assert.True(vm.CanContinue);
    }

    [Fact]
    public void BlankName_DisablesContinue()
    {
        var (vm, confirmed) = Create(detected: null);
        Assert.False(vm.CanContinue);
        Assert.False(vm.ContinueCommand.CanExecute(null));

        vm.DisplayName = "   ";
        Assert.False(vm.CanContinue);

        vm.DisplayName = "ElGee";
        Assert.True(vm.CanContinue);
        Assert.True(vm.ContinueCommand.CanExecute(null));
        Assert.Empty(confirmed);
    }

    [Fact]
    public void ChangingStreet_RebuildsHouseNumbersAndResets()
    {
        var (vm, _) = Create();
        vm.SelectedHouseNumber = 17;
        Assert.Equal(17, vm.SelectedHouseNumber);

        vm.SelectedStreet = vm.Streets[1];
        Assert.Equal("street-2", vm.SelectedStreet!.Id);
        Assert.Equal(1, vm.SelectedHouseNumber);
        Assert.Equal(33, vm.HouseNumbers.Count);

        vm.SelectedStreet = vm.Streets[0];
        Assert.Equal(1, vm.SelectedHouseNumber);
    }

    [Fact]
    public void Confirm_SavesTrimmedProfile()
    {
        var (vm, confirmed) = Create(detected: null);
        vm.DisplayName = "  ElGee  ";
        vm.SelectedStreet = vm.Streets[1];
        vm.SelectedHouseNumber = 17;

        vm.ContinueCommand.Execute(null);

        PlayerProfile saved = Assert.Single(confirmed);
        Assert.Equal("ElGee", saved.DisplayName);
        Assert.Equal("Germany", saved.Country);
        Assert.Equal("Brands", saved.City);
        Assert.Equal("street-2", saved.StreetId);
        Assert.Equal(17, saved.HouseNumber);
    }

    [Fact]
    public void SavedStreetMissing_FallsBackToFirstStreet()
    {
        var saved = new PlayerProfile("ElGee", "Germany", "Brands", "deleted-street", 99);
        var (vm, _) = Create(saved: saved);

        Assert.Equal("street-1", vm.SelectedStreet!.Id);
        Assert.Equal(1, vm.SelectedHouseNumber);
    }

    [Fact]
    public void EmptyMap_DisablesContinue()
    {
        var map = new AddressMap("Germany", "Brands", 1929.0, 30.0, []);
        var (vm, confirmed) = Create(map: map);

        Assert.Empty(vm.HouseNumbers);
        Assert.Null(vm.SelectedStreet);
        Assert.False(vm.CanContinue);
        vm.ContinueCommand.Execute(null);
        Assert.Empty(confirmed);
    }
}
