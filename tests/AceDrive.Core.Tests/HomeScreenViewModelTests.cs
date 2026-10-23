using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The home page (Unity YouScreen): the garage door moved here off the
/// jobs page, with the same teal voice acknowledgment as every other page.</summary>
public sealed class HomeScreenViewModelTests
{
    public HomeScreenViewModelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }

    private static (HomeScreenViewModel Vm, FakeClock Clock, VoiceBeat Beat) Create(
        Action? garage = null, Action? addresses = null, Action? back = null)
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        return (new HomeScreenViewModel(
            new RelayCommand(garage ?? (() => { })),
            new RelayCommand(addresses ?? (() => { })),
            new RelayCommand(back ?? (() => { })),
            beat), clock, beat);
    }

    [Fact]
    public void Voices_OfferTheGarageAddressesAndBack()
    {
        var (vm, _, _) = Create();
        Assert.Contains("GARAGE", vm.VoiceActions.Keys);
        Assert.Contains("ADDRESSES", vm.VoiceActions.Keys);
        Assert.Contains("BACK", vm.VoiceActions.Keys);
    }

    [Fact]
    public void VoiceAddresses_GlowsThenOpensThePageAfterTheBeat()
    {
        bool opened = false;
        var (vm, clock, beat) = Create(addresses: () => opened = true);

        vm.VoiceActions["ADDRESSES"].Invoke();
        Assert.True(vm.AddressesFlash);
        Assert.False(vm.GarageFlash);
        Assert.False(opened); // the acknowledgment owns the beat

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(opened);
        Assert.False(vm.AddressesFlash); // no stale glow to re-show later
    }

    [Fact]
    public void VoiceGarage_GlowsThenOpensTheGarageAfterTheBeat()
    {
        bool opened = false;
        var (vm, clock, beat) = Create(garage: () => opened = true);

        vm.VoiceActions["GARAGE"].Invoke();
        Assert.True(vm.GarageFlash);
        Assert.False(vm.BackFlash);
        Assert.False(opened); // the acknowledgment owns the beat

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(opened);
        Assert.False(vm.GarageFlash); // no stale glow to re-show later
    }

    [Fact]
    public void VoiceBack_GlowsThenReturnsToTheMenu()
    {
        bool backed = false;
        var (vm, clock, beat) = Create(back: () => backed = true);

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(backed);

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.True(backed);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void Click_ActsAtOnceAndDropsAPendingVoiceTap()
    {
        int opened = 0;
        var (vm, clock, beat) = Create(garage: () => opened++);

        vm.VoiceActions["GARAGE"].Invoke();
        vm.GarageCommand.Execute(null);

        Assert.Equal(1, opened);
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
        Assert.Equal(1, opened);
    }
}
