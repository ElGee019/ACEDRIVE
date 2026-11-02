using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The car-haul job headless (PORTPLAN item 20): the offer draw, ACCEPT, the
/// dealer pickup, the game-minute delivery leg and the settle. The assertions
/// read the same wallet and friendship store the flow writes; the fake host
/// records the pages and the pinned HUD leg in place of the shell.
/// </summary>
public sealed class CarHaulFlowTests
{
    // The receipt prints money in the current culture, and the suite shares
    // one process: pin en-US so a neighbour's language cannot leak in.
    public CarHaulFlowTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    [Fact]
    public void Open_DrawsTheOfferAndAcceptPinsTheDealer()
    {
        Harness h = Create();

        var page = Assert.IsType<CarHaulScreenViewModel>(h.Flow.Open(Back(), h.Host.ControlPage));
        Assert.True(page.IsOffer);
        Assert.Contains("Abarth", page.CarLine);

        page.AcceptCommand.Execute(null);

        Assert.True(h.Flow.Active);
        var job = Assert.IsType<CarHaulScreenViewModel>(h.Host.Pages[^1]);
        Assert.False(job.IsOffer);
        Assert.Contains("Bianchi Cars", job.TaskLine);
        Assert.Equal("Bianchi Cars", h.Host.Guidance!.TargetName);
        Assert.Equal(1, h.Host.Refreshes);
    }

    [Fact]
    public void ConfirmArrival_PicksUpThenSettlesTheHaul()
    {
        Harness h = Create();
        AcceptOffer(h);

        // PICK UP at the dealer: the car is loaded and the customer leg pins.
        h.Flow.ConfirmArrival();

        Assert.False(string.IsNullOrWhiteSpace(h.Host.Guidance!.TargetName));
        Assert.Equal("Street 2", h.Host.Guidance.Target.StreetName);

        // Drive 90 game minutes, then DELIVER THE CAR.
        h.Clock.Advance(TimeSpan.FromSeconds(GameClock.RealSecondsPerGameMinute * 90));
        h.Flow.ConfirmArrival();

        Assert.False(h.Flow.Active);
        var receipt = Assert.IsType<CarHaulReceiptScreenViewModel>(h.Host.Receipt);
        Assert.Equal(90, receipt.Receipt.DriveMinutes);
        Assert.True(receipt.Receipt.TotalEarned > 0.0);
        Assert.Equal(h.Wallet.Cash, receipt.Receipt.TotalEarned);
        Assert.Equal(1, h.Friends.GetLevel("Giulia Bianchi"));
        Assert.True(receipt.HasFriendship);
    }

    [Fact]
    public void ConfirmArrival_WithoutAnAcceptedOffer_DoesNothing()
    {
        Harness h = Create();
        h.Flow.ConfirmArrival();
        Assert.Null(h.Host.Receipt);
        Assert.Null(h.Host.Guidance);
    }

    [Fact]
    public void Open_WithoutADiscoveredDealer_OpensOneAndOffers()
    {
        // A career that has not discovered a dealer door yet still hauls: the
        // tap opens the dealer the offer rides from (Unity draws from every
        // dealership in the world) and banks it like any other door.
        Harness h = Create(withDealer: false);

        var page = Assert.IsType<CarHaulScreenViewModel>(
            h.Flow.Open(Back(), h.Host.ControlPage));

        Assert.True(page.IsOffer);
        Assert.Null(h.Host.Notice);
        Assert.Null(h.Host.HaulGate);
        Assert.Contains(
            h.Directory.All,
            poi => poi.Type == PoiTypes.Dealer);
    }

    [Fact]
    public void Open_WhenTheMapCannotOpenADealer_ShowsTheGenericHint()
    {
        Harness h = Create(withDealer: false, canOpenDealer: false);

        object page = h.Flow.Open(Back(), h.Host.ControlPage);

        Assert.Same(h.Host.ControlPage, page);
        Assert.Equal(Strings.Get("HaulUnavailable"), h.Host.Notice);
        // A map with no free house is world data, not progress: the button says
        // so in the generic words instead of naming a loyalty the player cannot
        // fix by driving.
        Assert.Equal(Strings.Get("ControlHaulUnavailable"), h.Host.HaulGate);
    }

    [Fact]
    public void Open_ForANeverDrivenPlayer_NamesTheLoyaltyGate()
    {
        // The one refusal the player can act on: no brand has been driven, so
        // the control page's own button says which loyalty is missing instead
        // of the generic hint.
        Harness h = Create(brandKm: new Dictionary<string, double>());

        object page = h.Flow.Open(Back(), h.Host.ControlPage);

        Assert.Same(h.Host.ControlPage, page);
        Assert.Null(h.Host.Notice);
        Assert.Equal(Strings.Get("ControlHaulLocked"), h.Host.HaulGate);
    }

    [Fact]
    public void Settle_WithoutADealerOwner_HidesTheFriendship()
    {
        Harness h = Create(dealerOwner: "");
        AcceptOffer(h);
        h.Flow.ConfirmArrival();
        h.Clock.Advance(TimeSpan.FromSeconds(GameClock.RealSecondsPerGameMinute * 30));
        h.Flow.ConfirmArrival();

        var receipt = Assert.IsType<CarHaulReceiptScreenViewModel>(h.Host.Receipt);
        Assert.False(receipt.HasFriendship);
        Assert.Empty(h.Friends.Names);
    }

    [Fact]
    public void Open_ReopensTheActiveLegInsteadOfDrawingAgain()
    {
        Harness h = Create();
        AcceptOffer(h);

        var page = Assert.IsType<CarHaulScreenViewModel>(h.Flow.Open(Back(), h.Host.ControlPage));

        Assert.False(page.IsOffer);
        Assert.Contains("Bianchi Cars", page.DealerLine);
    }

    private static Harness Create(
        bool withDealer = true,
        string dealerOwner = "Giulia Bianchi",
        IReadOnlyDictionary<string, double>? brandKm = null,
        bool canOpenDealer = true)
    {
        var clock = new FakeClock();
        var game = new GameClock(clock, clock.UtcNow);
        var wallet = new DriverWallet(new DriverState(0.0, 0.0));
        var friends = new FakeFriendshipStore();
        var host = new FakeCarHaulHost();
        var directory = new PoiDirectory();
        if (withDealer)
        {
            directory.Add(new PointOfInterest(
                "dealer-1", "Bianchi Cars",
                new AddressLocation("Street 1", 4, HouseSide.Left),
                PoiTypes.Dealer, dealerOwner));
        }

        var zone = new OperatingZone(
            new DriverState(0.0, 0.0, StreetIds: ["street-1", "street-2"], ZoneKm: 1.0),
            seed: 0, AddressMap.Default, homeStreetId: "street-1");
        var brands = new FakeBrandMileageStore(
            brandKm ?? new Dictionary<string, double> { ["Abarth"] = 60.0 });

        var flow = new CarHaulFlow(
            brands,
            [new CarSpec("ks_abarth_695", 2014, "Abarth", "695 Biposto", Class: 6)],
            directory, new FakeHaulDealerDoor(directory, canOpenDealer),
            zone, AddressMap.Default,
            () => new AddressLocation("Street 1", 1, HouseSide.Right),
            new ProceduralNameGenerator(NamePoolTable.Default, seed: 0),
            wallet, friends, game, TaxiRules.Default,
            new VoiceBeat(clock), new TestLog(),
            CarHaulPolicy.Development, new Random(1));
        flow.Attach(host);

        return new Harness(flow, wallet, friends, host, clock, directory);
    }

    private static void AcceptOffer(Harness h)
    {
        var page = Assert.IsType<CarHaulScreenViewModel>(h.Flow.Open(Back(), h.Host.ControlPage));
        page.AcceptCommand.Execute(null);
    }

    private static RelayCommand Back() => new(() => { });

    private sealed record Harness(
        CarHaulFlow Flow, DriverWallet Wallet, FakeFriendshipStore Friends,
        FakeCarHaulHost Host, FakeClock Clock, PoiDirectory Directory);
}
