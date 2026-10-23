using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The addresses page: discovered POIs alphabetical, a tap pins the
/// HUD's drive-to line, and BACK returns with the shared voice beat.</summary>
public sealed class AddressesScreenViewModelTests
{
    public AddressesScreenViewModelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }

    private static PointOfInterest Poi(string id, string name, int house = 4) =>
        new(id, name, new AddressLocation("Street 1", house, HouseSide.Right));

    private static AddressesScreenViewModel Create(
        PoiDirectory directory, FakeTelemetryHud? hud = null,
        FakeClock? clock = null, Action? back = null)
    {
        clock ??= new FakeClock();
        return new AddressesScreenViewModel(
            directory, hud, new RelayCommand(back ?? (() => { })),
            new VoiceBeat(clock));
    }

    [Fact]
    public void Rows_AreAlphabeticalAndCarryTheFormattedAddress()
    {
        var directory = new PoiDirectory();
        directory.Add(Poi("zeta", "Zeta"));
        directory.Add(Poi("bank", "Bank", house: 17));

        var vm = Create(directory);

        Assert.Equal(["Bank", "Zeta"], vm.Rows.Select(row => row.Name));
        Assert.Equal("Street 1 · No. 17", vm.Rows[0].Address);
        Assert.False(vm.IsEmpty);
        Assert.True(vm.HasRows);
    }

    [Fact]
    public void Select_PinsTheHudAndMarksTheRow()
    {
        var directory = new PoiDirectory();
        directory.Add(Poi("bank", "Bank"));
        directory.Add(Poi("dealer", "Car Dealer"));
        var hud = new FakeTelemetryHud();
        var vm = Create(directory, hud);

        AddressRow row = Assert.Single(vm.Rows, item => item.Name == "Car Dealer");
        vm.SelectCommand.Execute(row);

        Assert.Equal(row.Poi, hud.CurrentDestination);
        // The task text is handed to the companion at once, not only when the
        // panel closes.
        Assert.Equal(1, hud.ShowTaskTextCalls);
        Assert.True(Assert.Single(vm.Rows, item => item.Name == "Car Dealer").IsSelected);
        Assert.False(Assert.Single(vm.Rows, item => item.Name == "Bank").IsSelected);
        Assert.Contains("Task set:", vm.SelectedLine, StringComparison.Ordinal);
        Assert.Contains("Car Dealer", vm.SelectedLine, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_RowNamePinsTheSameRow()
    {
        var directory = new PoiDirectory();
        directory.Add(Poi("bank", "Bank"));
        var hud = new FakeTelemetryHud();
        var vm = Create(directory, hud);

        vm.VoiceActions["Bank"].Invoke();

        Assert.Equal("bank", hud.CurrentDestination!.Id);
        Assert.True(Assert.Single(vm.Rows).IsSelected);
    }

    [Fact]
    public void Voice_BackGlowsThenNavigatesAfterTheBeat()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool backed = false;
        var vm = new AddressesScreenViewModel(
            new PoiDirectory(), null, new RelayCommand(() => backed = true), beat);

        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(backed);

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();

        Assert.True(backed);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void Select_CancelsAPendingVoiceBack()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool backed = false;
        var directory = new PoiDirectory();
        directory.Add(Poi("bank", "Bank"));
        var vm = new AddressesScreenViewModel(
            directory, new FakeTelemetryHud(),
            new RelayCommand(() => backed = true), beat);

        vm.VoiceActions["BACK"].Invoke();
        vm.SelectCommand.Execute(Assert.Single(vm.Rows));

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();

        Assert.False(backed); // the newer row tap wins
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void EmptyDirectory_ShowsTheHint()
    {
        var vm = Create(new PoiDirectory());

        Assert.True(vm.IsEmpty);
        Assert.False(vm.HasRows);
        Assert.Empty(vm.Rows);
        Assert.Contains("No addresses", vm.EmptyLine, StringComparison.Ordinal);
    }
}
