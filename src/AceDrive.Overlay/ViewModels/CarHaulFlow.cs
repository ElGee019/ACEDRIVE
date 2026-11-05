using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The car-hauling job (PORTPLAN item 20, Unity JobSelectionScreen's car-haul
/// button + MapScreen's pickup/arrival branches): draws the offer, owns the
/// run's phase, pins each leg on the companion and settles the delivery into
/// the receipt. The shell reaches it through <see cref="ICarHaulHost"/>, so no
/// window, file or HWND lives here.
///
/// Time is game minutes (<see cref="GameClock.GameMinute"/>), never wall clock:
/// Unity stamps both delivery-leg ends on the game clock and pays the
/// difference. The loyalty and one-per-day rules live in
/// <see cref="CarHaulPolicy"/> so turning them on later is a composition-root
/// change, not a branch here. The pickup dealer is
/// <see cref="IHaulDealerDoor"/>'s: a career that has not discovered a dealer
/// door yet opens its first on the tap, so a fresh career can take a haul. The
/// flow never writes a POI itself.
/// </summary>
public sealed class CarHaulFlow
{
    private readonly IBrandMileageStore _brands;
    private readonly IReadOnlyList<CarSpec> _cars;
    private readonly PoiDirectory _pois;
    private readonly IHaulDealerDoor _dealerDoor;
    private readonly OperatingZone _zone;
    private readonly AddressMap _map;
    private readonly Func<AddressLocation?> _home;
    private readonly ResidentBook _residents;
    private readonly DriverWallet _wallet;
    private readonly JobMileageMeter _mileage;
    private readonly Action _saveDriver;
    private readonly IFriendshipStore _friendships;
    private readonly GameClock _game;
    private readonly TaxiRules _rules;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;
    private readonly CarHaulPolicy _policy;
    private readonly Random _rng;

    // WHY late: the host is the coordinator, built after this flow; the attach
    // closes in its constructor, before any page tap or arrival can reach here.
    private ICarHaulHost _host = null!;
    private ICommand _back = null!;
    private CarHaulOffer? _offer;
    private CarHaulRun? _run;
    // Session-only until the driver save carries it (the policy's daily cap is
    // off today): the stamp Unity writes on delivery.
    private DateTime? _lastDeliveredOn;

    public CarHaulFlow(
        IBrandMileageStore brands, IReadOnlyList<CarSpec> cars, PoiDirectory pois,
        IHaulDealerDoor dealerDoor, OperatingZone zone, AddressMap map,
        Func<AddressLocation?> home,
        ResidentBook residents, DriverWallet wallet, JobMileageMeter mileage,
        Action saveDriver, IFriendshipStore friendships,
        GameClock game, TaxiRules rules, VoiceBeat beat, ILog log,
        CarHaulPolicy policy, Random rng)
    {
        ArgumentNullException.ThrowIfNull(brands);
        ArgumentNullException.ThrowIfNull(cars);
        ArgumentNullException.ThrowIfNull(pois);
        ArgumentNullException.ThrowIfNull(dealerDoor);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(residents);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(mileage);
        ArgumentNullException.ThrowIfNull(saveDriver);
        ArgumentNullException.ThrowIfNull(friendships);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(rng);

        _brands = brands;
        _cars = cars;
        _pois = pois;
        _dealerDoor = dealerDoor;
        _zone = zone;
        _map = map;
        _home = home;
        _residents = residents;
        _wallet = wallet;
        _mileage = mileage;
        _saveDriver = saveDriver;
        _friendships = friendships;
        _game = game;
        _rules = rules;
        _beat = beat;
        _log = log;
        _policy = policy;
        _rng = rng;
    }

    public void Attach(ICarHaulHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _host = host;
    }

    /// <summary>True while an accepted haul is still in progress.</summary>
    public bool Active => _run is not null;

    /// <summary>
    /// The CAR HAULING tap: draws an offer when none is running, otherwise
    /// reopens the active leg's page. A refused tap keeps the control page and
    /// answers on that page's own button, in the words that fit the gate: the
    /// brand-loyalty floor is progress the player can make, so it is named; a
    /// missing dealer or customer door (and the daily cap) keep the generic
    /// line, because more driving does not fix those.
    /// </summary>
    public object Open(ICommand back, object controlPage)
    {
        ArgumentNullException.ThrowIfNull(back);
        ArgumentNullException.ThrowIfNull(controlPage);
        _back = back;

        if (_run is null)
        {
            if (!_policy.CanOffer(_game.GameNow, _lastDeliveredOn, haulInProgress: false))
            {
                _log.LogInfo("haul refused: today's haul is already delivered");
                _host.ShowHaulGate(Strings.Get("ControlHaulUnavailable"));
                return controlPage;
            }

            CarHaulDraw draw = DrawOffer();
            if (draw.Offer is null && !draw.BlockedByBrandLoyalty)
            {
                // One gap the job closes itself: a career without a dealer door
                // opens the one this haul rides from (Unity draws from every
                // dealership in the world), then draws once more. The loyalty
                // floor opens nothing: that refusal is progress the player owes,
                // and a dealer door appearing would not help them.
                if (_dealerDoor.Dealer() is not null)
                {
                    draw = DrawOffer();
                }
            }

            if (draw.Offer is null)
            {
                Refuse(draw.BlockedByBrandLoyalty);
                return controlPage;
            }
        }

        return BuildPage();
    }

    // A refused tap answers on the control page's own button: the loyalty floor
    // in its own words, every other gap in the generic line plus the
    // companion's notice (the one place that can explain a world-data gap).
    private void Refuse(bool brandLoyaltyGate)
    {
        if (brandLoyaltyGate)
        {
            _log.LogInfo("haul refused: no brand reaches the loyalty floor");
            _host.ShowHaulGate(Strings.Get("ControlHaulLocked"));
            return;
        }

        _log.LogInfo("haul refused: no offer could be drawn");
        _host.ShowNotice(Strings.Get("HaulUnavailable"));
        _host.ShowHaulGate(Strings.Get("ControlHaulUnavailable"));
    }

    /// <summary>
    /// The companion's ENTER at the pinned leg: PICK UP at the dealer, then
    /// DELIVER THE CAR at the customer. Called from the one HUD event, so a
    /// parked car confirms exactly the leg the companion is showing.
    /// </summary>
    public void ConfirmArrival()
    {
        if (_run is null)
        {
            return;
        }

        if (_run.Phase == CarHaulPhase.Offered)
        {
            if (_run.PickUp(_run.Offer.Dealer.Id))
            {
                // Unity stamps the delivery leg when DRIVE is pressed on the
                // customer marker; the overlay has no separate DRIVE page, so
                // the pickup itself starts the leg before the player moves.
                _run.BeginDelivery(_game.GameMinute);
                PinCustomer();
                _host.ShowPage(BuildPage());
                _host.RefreshControl();
                _log.LogInfo($"haul picked up: {_run.Offer.Car.Brand} " +
                    $"{_run.Offer.Car.Model} at {_run.Offer.Dealer.Name}");
            }
            return;
        }

        if (_run.Phase == CarHaulPhase.PickedUp)
        {
            Settle();
        }
    }

    // The loyalty floor answers "no brand the player has driven clears it",
    // which the control page prints on the button; every other empty pool is a
    // world-data gap the player cannot act on (see CarHaulDraw). The customer
    // is a resident of the drawn door, banked like a taxi passenger, so the
    // same house always names the same person and never hosts a second one.
    private CarHaulDraw DrawOffer()
    {
        AddressLocation? home = _home();
        CarHaulDraw draw = new CarHaulOfferService(_rng).TryDraw(
            _cars, _pois.All, _zone.ZoneMap, home, LoyaltyPercent,
            door => _residents.GetOrCreate(door).Name, _policy,
            _pois.HasDoor);
        _offer = draw.Offer;
        return draw;
    }

    // Unity's BrandLoyaltyPct: never-driven brands answer 0 (not the curve's
    // 1% floor), so the policy's "any driven brand" reading stays honest.
    private double LoyaltyPercent(string brand)
    {
        double km = _brands.GetKm(brand);
        return km > 0.0 ? BrandLoyalty.Rate(km) * 100.0 : 0.0;
    }

    private void Accept()
    {
        if (_offer is null)
        {
            return;
        }

        _run = new CarHaulRun(_offer);
        _offer = null;
        // The two legs' odometer starts here; the settle banks the delta.
        _mileage.StartJob();
        PinDealer();
        _host.ShowPage(BuildPage());
        _host.RefreshControl();
        _log.LogInfo($"haul accepted: {_run.Offer.Car.Brand} {_run.Offer.Car.Model} " +
            $"from {_run.Offer.Dealer.Name}, tier {_run.Offer.PayoutTier}");
    }

    private void Settle()
    {
        CarHaulRun run = _run!;
        if (!run.Deliver(_game.GameMinute))
        {
            return;
        }

        CarHaulOffer offer = run.Offer;
        // Unity credits the dealer POI's friendship by one; a job without a
        // dealer on the map (or an ownerless row) settles with the box hidden
        // instead of inventing a stranger.
        FriendshipResult? friendship = null;
        if (offer.Dealer.OwnerName.Length > 0)
        {
            FriendshipOutcome outcome = _friendships.RegisterRide(offer.Dealer.OwnerName);
            friendship = new FriendshipResult(
                outcome.OwnerName, outcome.Level, outcome.TierIncreased);
        }

        int rating = (int)Math.Round(
            _wallet.Rating.Overall, MidpointRounding.AwayFromZero);
        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            CarDisplay(offer.Car),
            offer.Car.Class,
            rating,
            run.DriveMinutes,
            friendship?.OwnerName ?? "",
            friendship?.Level ?? 0,
            friendship?.TierIncreased ?? false);

        _mileage.FinishJob();
        _wallet.AddHaul(receipt);
        // The haul's receipt is the only bank this job path writes; the
        // odometer delta rides it with the cash.
        _saveDriver();
        _lastDeliveredOn = _game.GameNow;
        _run = null;
        _host.PinTarget(null);
        _host.ShowReceipt(new CarHaulReceiptScreenViewModel(
            receipt, _wallet, _beat, _back));
        _host.RefreshControl();
        _log.LogInfo($"haul settled: {receipt.CarName} minutes={receipt.DriveMinutes} " +
            $"tier={receipt.Tier} earned={receipt.TotalEarned:0.00} " +
            $"wallet={_wallet.Cash:0.00}");
    }

    private void PinDealer()
    {
        CarHaulOffer offer = _run!.Offer;
        _host.PinTarget(new HudJobGuidance(
            offer.Dealer.Name,
            offer.Dealer.Address,
            "HudHaulToDealer",
            "HudHaulArrivedDealer",
            "HudPickUp",
            _rules.ArrivalRadiusMeters,
            DriveArgs: [CarDisplay(offer.Car)]));
    }

    private void PinCustomer()
    {
        CarHaulOffer offer = _run!.Offer;
        _host.PinTarget(new HudJobGuidance(
            offer.Customer.BaseName,
            offer.CustomerAddress,
            "HudHaulToCustomer",
            "HudHaulArrivedCustomer",
            "HudHaulDeliverCar",
            _rules.ArrivalRadiusMeters));
    }

    private CarHaulScreenViewModel BuildPage()
    {
        CarHaulOffer offer = _run?.Offer ?? _offer!;
        return new CarHaulScreenViewModel(
            offer,
            _run?.Phase ?? CarHaulPhase.Offered,
            _map.City,
            new RelayCommand(Accept),
            _back,
            _beat,
            isOffer: _run is null);
    }

    private static string CarDisplay(CarSpec car) => $"{car.Brand} {car.Model}";
}
