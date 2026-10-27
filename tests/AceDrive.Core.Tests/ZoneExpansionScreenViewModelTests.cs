using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The operating-zone expansion page (TOZONES §4.5): the growth report's copy,
/// the businesses it opened with their resx type labels, and the two ways out
/// (CONTINUE hands over, FIND STREET opens the map stand-in). Copy is asserted
/// through the resx, so a translator's edit is what the page shows.
/// </summary>
public sealed class ZoneExpansionScreenViewModelTests
{
    // The page formats km and looks copy up through the current culture: pin
    // both to en-US like every other page suite, or a Dutch machine renders
    // "1,9 km" and the Dutch labels.
    public ZoneExpansionScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture =
            new CultureInfo("en-US");

    private static ZoneExpansion Expansion => new(
        "Street 6", StreetCount: 6, PreviousKm: 1.929, NewKm: 2.8935, JobsToNext: 3);

    private static PointOfInterest Poi(string type, string name) => new(
        $"{type}_0", name,
        new AddressLocation("Street 6", 1, HouseSide.Right), type);

    private static (ZoneExpansionScreenViewModel Vm, bool[] Continued, bool[] Found) Create(
        IReadOnlyList<PointOfInterest>? added = null, FakeZoneStandings? standings = null)
    {
        var beat = new VoiceBeat(new FakeClock());
        var continued = new bool[1];
        var found = new bool[1];
        var vm = new ZoneExpansionScreenViewModel(
            Expansion,
            added ?? [Poi(PoiTypes.CarWash, "Ace's Carwash")],
            standings ?? new FakeZoneStandings(),
            beat,
            new RelayCommand(() => continued[0] = true),
            new RelayCommand(() => found[0] = true));
        return (vm, continued, found);
    }

    [Fact]
    public void Lines_ComeFromTheExpansionAndTheResx()
    {
        var (vm, _, _) = Create();

        Assert.Equal(Strings.Get("ZoneExpansionTitle"), vm.Title);
        Assert.Equal("OPERATING ZONE EXPANDED", vm.Title);
        Assert.Equal("STREET 6", vm.StreetLine);
        Assert.Equal("Operating Zone: 6 streets", vm.ZoneLine);
        Assert.Equal("Previous Area: 1.9 km", vm.PreviousAreaLine);
        Assert.Equal("New Total Area: 2.9 km", vm.NewAreaLine);
        Assert.Equal("Jobs to next expansion: 3", vm.JobsToNextLine);
    }

    [Fact]
    public void BusinessLines_ListTheSpawnedDoorsInOrderWithTheirTypeLabels()
    {
        var (vm, _, _) = Create(
        [
            Poi(PoiTypes.CarWash, "Ace's Carwash"),
            Poi(PoiTypes.Bank, "Rae's Bank"),
            Poi(PoiTypes.GroceryStore, "Noor's Grocery"),
        ]);

        Assert.True(vm.HasBusinesses);
        Assert.Equal(
            ["Car Wash: Ace's Carwash", "Bank: Rae's Bank", "Grocery Store: Noor's Grocery"],
            vm.BusinessLines);
    }

    [Fact]
    public void WithoutBusinesses_NoRowsAndTheSectionHides()
    {
        // The early thresholds only name a street; the page must not show an
        // empty "new businesses" heading.
        var (vm, _, _) = Create([]);

        Assert.False(vm.HasBusinesses);
        Assert.Empty(vm.BusinessLines);
    }

    [Theory]
    [InlineData(PoiTypes.CarWash, "Car Wash")]
    [InlineData(PoiTypes.Bank, "Bank")]
    [InlineData(PoiTypes.Insurance, "Insurance")]
    [InlineData(PoiTypes.Dealer, "Car Dealer")]
    [InlineData(PoiTypes.Mechanic, "Mechanic")]
    [InlineData(PoiTypes.GroceryStore, "Grocery Store")]
    [InlineData(PoiTypes.CoffeeShop, "Coffee Shop")]
    public void PoiTypeLabels_MapEveryZoneFamily(string type, string label) =>
        Assert.Equal(label, PoiTypeLabels.Of(type));

    [Fact]
    public void PoiTypeLabels_UnknownTag_FallsBackToTitleCase()
    {
        Assert.Equal("Maxs Grocery Store", PoiTypeLabels.Of(PoiTypes.MaxsGrocery));
        Assert.Equal("Special Store", PoiTypeLabels.Of("special_store"));
        Assert.Equal(string.Empty, PoiTypeLabels.Of(string.Empty));
    }

    [Fact]
    public void Standings_ShowTheRankAndDeltaInResxCopy()
    {
        var standings = new FakeZoneStandings
        {
            Result = new ZoneStandingsResult(2, 9, 1, 3, IsFirstTime: false),
        };
        var (vm, _, _) = Create(standings: standings);

        Assert.True(vm.Standings.IsVisible);
        Assert.Equal("2nd of 9.", vm.Standings.Line);
        Assert.Equal(Strings.Get("ReceiptStandingsGainedOne"), vm.Standings.DeltaLine);
        Assert.True(vm.Standings.Changed);
    }

    [Fact]
    public void Standings_WithoutABackend_HideTheWholeBlock()
    {
        // Offline is Unity's build with rivals compiled out: no block, no
        // placeholder waiting for a board that will never land.
        var (vm, _, _) = Create(standings: new FakeZoneStandings { IsAvailable = false });

        Assert.False(vm.Standings.IsVisible);
        Assert.Equal("—/—", vm.Standings.Line);
        Assert.Equal(string.Empty, vm.Standings.DeltaLine);
    }

    [Fact]
    public void Standings_FollowALateBoardResult()
    {
        var standings = new FakeZoneStandings();
        var (vm, _, _) = Create(standings: standings);
        Assert.Equal("—/—", vm.Standings.Line);

        standings.Emit(new ZoneStandingsResult(4, 12, 0, 4, IsFirstTime: false));

        Assert.Equal("4th of 12.", vm.Standings.Line);
        Assert.Equal(Strings.Get("ReceiptStandingsNoChange"), vm.Standings.DeltaLine);
    }

    [Fact]
    public void FirstResult_ShowsTheRankButNoDelta()
    {
        // Unity's isFirstTime branch: there is no earlier rank to have moved
        // from, so the delta line stays empty.
        var standings = new FakeZoneStandings
        {
            Result = new ZoneStandingsResult(1, 1, 0, 1, IsFirstTime: true),
        };
        var (vm, _, _) = Create(standings: standings);

        Assert.Equal("1st of 1.", vm.Standings.Line);
        Assert.Equal(string.Empty, vm.Standings.DeltaLine);
        Assert.False(vm.Standings.Changed);
    }

    [Fact]
    public void LeavingThePage_UnhooksTheBoard()
    {
        var standings = new FakeZoneStandings();
        var (vm, _, _) = Create(standings: standings);

        vm.FindStreetCommand.Execute(null);
        standings.Emit(new ZoneStandingsResult(3, 9, 2, 5, IsFirstTime: false));

        Assert.Equal("—/—", vm.Standings.Line);
    }

    [Fact]
    public void Continue_ExecutesTheContinuationAndClearsTheFlash()
    {
        var (vm, continued, found) = Create();
        vm.ContinueCommand.Execute(null);

        Assert.True(continued[0]);
        Assert.False(found[0]);
        Assert.False(vm.ContinueFlash);
        Assert.False(vm.FindStreetFlash);
    }

    [Fact]
    public void FindStreet_ExecutesItsCommand()
    {
        var (vm, continued, found) = Create();
        vm.FindStreetCommand.Execute(null);

        Assert.False(continued[0]);
        Assert.True(found[0]);
    }

    [Fact]
    public void VoiceTaps_AnswerToTheirLabels_AndWaitOutTheBeat()
    {
        var (vm, continued, found) = Create();

        Assert.True(vm.VoiceActions.TryGetValue(
            Strings.Get("ZoneExpansionContinue"), out Action? continueAction));
        continueAction!();
        Assert.True(vm.ContinueFlash);
        Assert.False(continued[0]);

        Assert.True(vm.VoiceActions.TryGetValue(
            Strings.Get("ZoneExpansionFindStreet"), out Action? findAction));
        findAction!();
        Assert.True(vm.FindStreetFlash);
        Assert.False(found[0]);
    }

    [Fact]
    public void Page_KeepsThePanelOpen_AndIsAVoiceScreen()
    {
        var (vm, _, _) = Create();
        Assert.IsAssignableFrom<IKeepPanelOpen>(vm);
        Assert.IsAssignableFrom<IVoiceScreen>(vm);
    }
}
