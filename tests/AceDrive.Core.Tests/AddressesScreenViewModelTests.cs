using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The addresses page: the operating zone's readout on top, the
/// discovered POIs alphabetical under it, a tap pins the HUD's drive-to line,
/// and BACK returns with the shared voice beat.</summary>
public sealed class AddressesScreenViewModelTests
{
    public AddressesScreenViewModelTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
    }

    private static PointOfInterest Poi(string id, string name, int house = 4) =>
        new(id, name, new AddressLocation("Street 1", house, HouseSide.Right));

    // The zone the root composes for a fresh profile: the placeholder map with
    // only the home street registered and the interim area seeded.
    private static OperatingZone FreshZone()
    {
        AddressMap map = AddressMap.Default;
        return new OperatingZone(
            null, seed: 0, map, initialKm: AddressBook.MapLengthKm(map));
    }

    private static AddressesScreenViewModel Create(
        PoiDirectory directory, FakeTelemetryHud? hud = null,
        FakeClock? clock = null, Action? back = null, OperatingZone? zone = null)
    {
        clock ??= new FakeClock();
        return new AddressesScreenViewModel(
            directory, zone ?? FreshZone(), hud,
            new RelayCommand(back ?? (() => { })), new VoiceBeat(clock));
    }

    [Fact]
    public void Zone_ShowsTheStreetCountTheAreaAndTheJobsToTheNextExpansion()
    {
        var vm = Create(new PoiDirectory());

        Assert.Equal(Strings.Format("AddressesZoneLine", 1, 1.929), vm.ZoneLine);
        Assert.Contains("1 streets", vm.ZoneLine, StringComparison.Ordinal);
        Assert.Contains("1.9 km", vm.ZoneLine, StringComparison.Ordinal);
        Assert.Equal(["Street 1"], vm.StreetNames);
        Assert.Equal(Strings.Format("AddressesZoneNext", 2), vm.NextExpansionLine);
        Assert.Equal(Strings.Get("AddressesStreetsLabel"), vm.StreetsLabel);
    }

    [Fact]
    public void Zone_ListsTheStreetsInTheZoneOrder_NotTheMapOrder()
    {
        // Legacy save ids ("home", "s1") only supply the count; the profile's
        // home street leads the list exactly like the zone's draws.
        var zone = new OperatingZone(
            new DriverState(0.0, 0.0, StreetIds: ["home", "s1"], ZoneKm: 2.8935),
            seed: 0, AddressMap.Default, homeStreetId: "street-2");

        var vm = Create(new PoiDirectory(), zone: zone);

        Assert.Equal(["Street 2", "Street 1"], vm.StreetNames);
        Assert.Equal(Strings.Format("AddressesZoneLine", 2, 2.8935), vm.ZoneLine);
    }

    [Fact]
    public void Zone_CompletePool_NamesTheCompletionInsteadOfMoreJobs()
    {
        var zone = new OperatingZone(
            new DriverState(0.0, 0.0, StreetIds: ["street-1", "street-2"], ZoneKm: 3.858),
            seed: 0, AddressMap.Default);

        var vm = Create(new PoiDirectory(), zone: zone);

        Assert.Equal(["Street 1", "Street 2"], vm.StreetNames);
        Assert.Equal(Strings.Get("AddressesZoneComplete"), vm.NextExpansionLine);
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
            new PoiDirectory(), FreshZone(), null,
            new RelayCommand(() => backed = true), beat);

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
            directory, FreshZone(), new FakeTelemetryHud(),
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
