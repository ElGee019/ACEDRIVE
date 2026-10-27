using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The weekly missing-person run (ToWhereIsWilly.md, port of Unity
/// WhereIsWillyHandler + JobSelectionScreen's Wednesday button + MapScreen's
/// task lines): the calendar's Wednesday row draws a hiding coffee shop and a
/// missing elder, each wrong shop costs an attempt, finding the elder pays a
/// finder's fee raised when the player parks at their home, and the DROP OFF
/// pays, stamps the Wednesday and leaves the certificate in the trunk.
///
/// Everything here is session state (Unity parity): only the completion stamp
/// lives in <see cref="WhereIsWillyLedger"/>, so a restart, a breakdown or a
/// wrong address throws the search away without a receipt and the row reopens.
/// The map is a page until the marker network lands (adaptation A1): the quest
/// page lists the town's coffee shops, and the companion carries the pinned
/// door. Arrival rides the one 10 Hz poll through
/// <see cref="ITelemetryHud.ParkedAtTarget"/>, never a timer of its own.
/// </summary>
public sealed class WhereIsWillyFlow
{
    // The HUD's one-shot notice beat, the same length the valuation results
    // and the taxi refusals use.
    private const double NoticeSeconds = 4.0;

    // The two temporary marker ids. The HUD only ever shows one of them at a
    // time, and the quest owns both, so the ids never meet a directory row.
    private const string HomeMarkerId = "willy-home";
    private const string WrongMarkerId = "willy-wrong";

    private readonly AddressMap _map;
    private readonly GameClock _game;
    private readonly PoiGenerator _generator;
    private readonly PoiDirectory _directory;
    private readonly ResidentBook _residents;
    private readonly Garage _garage;
    private readonly IReadOnlyDictionary<string, CarSpec> _catalog;
    private readonly WhereIsWillyRules _rules;
    private readonly WhereIsWillyLedger _ledger;
    private readonly IFriendshipStore _friendships;
    private readonly Trunk _trunk;
    private readonly DriverWallet _wallet;
    private readonly ITelemetryHud? _hud;
    private readonly GameStateSaver _saver;
    private readonly IRivalsService _rivals;
    private readonly VoiceBeat _beat;
    private readonly Func<int> _displayWeek;
    private readonly Random _rng;
    private readonly ILog _log;

    // WHY late: the host is the coordinator, which is built after this flow.
    // The attach closes in the coordinator's constructor, before any calendar
    // tap or arrival can reach it.
    private IWhereIsWillyHost _host = null!;

    private WhereIsWillyPhase _phase = WhereIsWillyPhase.Idle;
    private PointOfInterest? _shop;
    private Resident? _elder;
    private AddressLocation _home;
    private int _shopCount;
    private int _attempts;
    private bool _missed;
    private WhereIsWillyReceipt? _receipt;

    public WhereIsWillyFlow(
        AddressMap map, GameClock game, PoiGenerator generator,
        PoiDirectory directory, ResidentBook residents, Garage garage,
        IReadOnlyList<CarSpec> catalog, WhereIsWillyRules rules,
        WhereIsWillyLedger ledger, IFriendshipStore friendships, Trunk trunk,
        DriverWallet wallet, ITelemetryHud? hud, GameStateSaver saver,
        IRivalsService rivals, VoiceBeat beat, Func<int> displayWeek,
        Random rng, ILog log)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(residents);
        ArgumentNullException.ThrowIfNull(garage);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(friendships);
        ArgumentNullException.ThrowIfNull(trunk);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(saver);
        ArgumentNullException.ThrowIfNull(rivals);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(displayWeek);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(log);

        _map = map;
        _game = game;
        _generator = generator;
        _directory = directory;
        _residents = residents;
        _garage = garage;
        var byKey = new Dictionary<string, CarSpec>(StringComparer.Ordinal);
        foreach (CarSpec car in catalog)
        {
            byKey.TryAdd(car.Key, car);
        }
        _catalog = byKey;
        _rules = rules;
        _ledger = ledger;
        _friendships = friendships;
        _trunk = trunk;
        _wallet = wallet;
        _hud = hud;
        _saver = saver;
        _rivals = rivals;
        _beat = beat;
        _displayWeek = displayWeek;
        _rng = rng;
        _log = log;
    }

    /// <summary>
    /// True from the first shop tap until the receipt or CONTINUE closes the
    /// run. The calendar's other event rows, the taxi shift and the delivery
    /// career read it: Unity's job-centre lock, ported to the actions that
    /// could start a second job.
    /// </summary>
    public bool InFlight => _phase != WhereIsWillyPhase.Idle;

    /// <summary>Where the run is; session state, never persisted (§2.9).</summary>
    public WhereIsWillyPhase Phase => _phase;

    /// <summary>Attaches the coordinator that owns the panel and the page policy.</summary>
    public void Attach(IWhereIsWillyHost host) => _host = host;

    /// <summary>
    /// The calendar's Wednesday tap (Unity's job-centre button). The gate is
    /// re-checked here as well as by the calendar page, so a caller can never
    /// jump it. A run already in flight reopens its page instead of drawing a
    /// second search; the two terminal phases hold their last step on the HUD.
    /// </summary>
    public void Begin()
    {
        DateTime now = _game.GameNow;
        if (!WhereIsWillyRules.IsWindow(ControlCalendar.ToDayOfWeek(now), now.Hour))
        {
            _log.LogInfo("where is willy ignored: outside the Wednesday 8am-8pm window");
            return;
        }
        if (_ledger.CompletedOn(now))
        {
            _log.LogInfo("where is willy ignored: this Wednesday is already stamped");
            return;
        }
        if (_phase is WhereIsWillyPhase.Searching or WhereIsWillyPhase.Found)
        {
            _host.PresentQuest(CreateQuestPage());
            return;
        }
        if (InFlight)
        {
            _log.LogInfo("where is willy ignored: the HUD holds DROP OFF or CONTINUE");
            return;
        }

        IReadOnlyList<PointOfInterest> shops = WhereIsWillyCandidates.CoffeeShops(_directory.All);
        if (shops.Count == 0)
        {
            _log.LogWarning("where is willy not drawn: no coffee shop in town");
            return;
        }
        if (!Draw(shops))
        {
            return;
        }

        _phase = WhereIsWillyPhase.Searching;
        _shopCount = shops.Count;
        _attempts = 0;
        _missed = false;
        _receipt = null;
        _host.PresentQuest(CreateQuestPage());
        _log.LogInfo(
            $"where is willy started: {_elder?.Name.Full} missing in {_map.City}, " +
            $"{shops.Count} coffee shop(s) to search");
    }

    /// <summary>
    /// A row on the search page: pins that coffee shop and sends the player
    /// off. Unity's two taps (select the marker, then DRIVE) are one here.
    /// </summary>
    public void ChooseCoffeeShop(PointOfInterest? shop)
    {
        if (_phase != WhereIsWillyPhase.Searching || shop is null)
        {
            return;
        }

        _hud?.SetDestination(shop, new HudDestinationCopy("HudWillySearchDrive"));
        _host.HidePanel();
        _log.LogInfo(
            $"where is willy: driving to {shop.Name} at {AddressFormatter.Format(shop.Address)}");
    }

    /// <summary>
    /// The found page's DRIVE HOME row: pins the elder's door. Unity's
    /// street-name markers have no port equivalent, so the page names the
    /// address and the companion pins the exact house (adaptation A2).
    /// </summary>
    public void ChooseHome()
    {
        if (_phase != WhereIsWillyPhase.Found || _elder is null)
        {
            return;
        }

        _hud?.SetDestination(HomeMarker(), HomeCopy());
        _host.HidePanel();
        _log.LogInfo(
            $"where is willy: driving {_elder.Name.FirstName} home to " +
            $"{AddressFormatter.Format(_home)}");
    }

    /// <summary>
    /// The one poll hook, after the HUD's update: a parked car at a coffee
    /// shop during the search finds the elder (or costs an attempt), and a
    /// parked car at the elder's door during leg 2 brings them home. The
    /// terminal phases are resolved by ENTER, never here, so neither the fee
    /// nor the penalty can run twice.
    /// </summary>
    public void Advance(TelemetrySample? sample)
    {
        if (sample is null || _hud is null)
        {
            return;
        }

        if (_phase is WhereIsWillyPhase.Searching)
        {
            if (!_hud.ParkedAtTarget || _hud.CurrentDestination is not { } shop)
            {
                return;
            }
            if (!IsCoffeeShop(shop))
            {
                // A non-coffee-shop door keeps its own handler: the place
                // opens as usual and the quest does not react.
                return;
            }
            if (_shop is { } hiding && WhereIsWillyRules.SameDoor(shop.Address, hiding.Address))
            {
                Find();
            }
            else
            {
                Miss();
            }
            return;
        }

        if (_phase is WhereIsWillyPhase.Found)
        {
            if (!_hud.ParkedAtTarget || _hud.CurrentDestination is not { } door)
            {
                return;
            }
            if (WhereIsWillyRules.SameDoor(door.Address, _home))
            {
                HomeArrived();
            }
            else
            {
                WrongAddress(door);
            }
        }
    }

    /// <summary>
    /// ENTER on a quest marker (the flow's <see cref="IPoiArrivalHandler"/>,
    /// reached through <see cref="WhereIsWillyPoiHandler"/>): the home door
    /// pays, stamps and hands back the receipt page, the wrong-address door
    /// clears the run unpaid. Anything else is not the quest's to answer.
    /// </summary>
    public object? Enter(PointOfInterest? poi)
    {
        if (poi is null)
        {
            return null;
        }

        if (_phase is WhereIsWillyPhase.AtHome && WhereIsWillyRules.SameDoor(poi.Address, _home))
        {
            return Complete();
        }

        if (_phase is WhereIsWillyPhase.WrongAddress)
        {
            Reset();
            _hud?.SetDestination(null);
            _host.HidePanel();
            _log.LogInfo("where is willy: wrong address acknowledged, the run ends unpaid");
        }

        return null;
    }

    /// <summary>
    /// GIVE UP SEARCH (only offered after a failed visit): clears the run with
    /// no fee and no stamp, leaves Unity's abandoned sentence on the companion
    /// and hides the page. The next calendar tap draws a fresh search.
    /// </summary>
    public void Abandon()
    {
        if (_phase != WhereIsWillyPhase.Searching || _attempts <= 0 || _elder is null)
        {
            return;
        }

        string firstName = _elder.Name.FirstName;
        Reset();
        _host.HidePanel();
        _hud?.ShowNotice(
            Strings.Format("HudWillyAbandoned", firstName),
            TimeSpan.FromSeconds(NoticeSeconds));
        _log.LogInfo("where is willy abandoned: no fee, no stamp");
    }

    /// <summary>The receipt's CONTINUE: the quest is over, back to the control page.</summary>
    public void Continue()
    {
        Reset();
        _host.ShowControl();
    }

    /// <summary>
    /// The quest page's BACK: hides the panel and keeps the run alive, so the
    /// calendar row reopens it. Unity has no such verb because its map never
    /// leaves the screen; this is the port's one page affordance.
    /// </summary>
    public void Back() => _host.HidePanel();

    // The draw (Unity JobSelectionScreen's Wednesday button): one hiding shop,
    // then one elder — a resident no venue owns, or a freshly banked one when
    // the resident table is still empty (adaptation A3).
    private bool Draw(IReadOnlyList<PointOfInterest> shops)
    {
        int index = Math.Clamp(_rng.Next(shops.Count), 0, shops.Count - 1);
        _shop = shops[index];

        IReadOnlyList<Resident> candidates =
            WhereIsWillyCandidates.EligibleResidents(_residents.Residents, VenueOwners());
        _elder = WhereIsWillyCandidates.Pick(candidates, _rng.Next) ?? DrawFreeResident();
        if (_elder is null)
        {
            _shop = null;
            _log.LogWarning("where is willy not drawn: no resident and no free house");
            return false;
        }

        _home = _elder.Address;
        return true;
    }

    // Every owner a discovered door names: a shop or hub owner can never be
    // the missing elder (Unity's excluded-owner set).
    private List<string> VenueOwners()
    {
        List<string> owners = [];
        foreach (PointOfInterest poi in _directory.All)
        {
            if (!string.IsNullOrWhiteSpace(poi.OwnerName))
            {
                owners.Add(poi.OwnerName);
            }
        }
        return owners;
    }

    // The empty-book fallback: a free house that no door and no resident
    // already uses, banked as a resident in the same call (GetOrCreate saves),
    // so the elder the receipt names is the one the next launch reopens.
    private Resident? DrawFreeResident()
    {
        List<AddressLocation> taken = [];
        foreach (PointOfInterest poi in _directory.All)
        {
            taken.Add(poi.Address);
        }
        foreach (Resident resident in _residents.Residents)
        {
            taken.Add(resident.Address);
        }

        return WhereIsWillyCandidates.PickFreeHouse(_map, _generator, taken) is { } door
            ? _residents.GetOrCreate(door)
            : null;
    }

    // The hiding shop's arrival: the fee is priced now (Unity CalculateReward),
    // the pinned shop is released so a stray ENTER cannot open it, and the page
    // reports the run's one good news.
    private void Find()
    {
        _phase = WhereIsWillyPhase.Found;
        _missed = false;
        _hud?.SetDestination(null);
        _receipt = BuildReceipt();
        _host.PresentQuest(CreateQuestPage());
        _log.LogInfo(
            $"where is willy found at {_shop?.Name}: fee {_receipt.FinderFee} CR " +
            $"after {_attempts} miss(es)");
    }

    // Any other coffee shop: one attempt is spent, the pin is released and the
    // page offers the abandoned search.
    private void Miss()
    {
        string name = _hud?.CurrentDestination?.Name ?? "the shop";
        _attempts++;
        _missed = true;
        _hud?.SetDestination(null);
        _host.PresentQuest(CreateQuestPage());
        _log.LogInfo($"where is willy: nobody at {name} (attempt {_attempts})");
    }

    // Leg 2's arrival: the elder is home, the friendship moves +1, and the HUD
    // keeps the door pinned with DROP OFF. The fee is paid by that tap, never
    // here.
    private void HomeArrived()
    {
        if (_elder is null)
        {
            return;
        }

        FriendshipOutcome outcome = _friendships.RegisterRide(_elder.Name.BaseName);
        _receipt = _receipt is { } priced ? priced with { FriendshipLevel = outcome.Level } : null;
        _phase = WhereIsWillyPhase.AtHome;
        _hud?.SetDestination(HomeMarker(), HomeCopy());
        _log.LogInfo(
            $"where is willy: {_elder.Name.FirstName} home, friendship {outcome.Level}");
    }

    // Any other door with the elder on board: the friendship moves -1 and the
    // HUD offers CONTINUE, which throws the run away without a stamp.
    private void WrongAddress(PointOfInterest door)
    {
        if (_elder is null)
        {
            return;
        }

        _friendships.AddFamiliarity(_elder.Name.BaseName, -1);
        _phase = WhereIsWillyPhase.WrongAddress;
        // WHY the drive-home copy here: the marker sits at the door the player
        // parked at (that is what keeps ENTER reachable), and the port has one
        // drive-to sentence for the elder; only the arrival line and the verb
        // change for the wrong address.
        _hud?.SetDestination(
            new PointOfInterest(WrongMarkerId, _elder.Name.FirstName, door.Address, PoiTypes.Willy),
            new HudDestinationCopy(
                "HudWillyHomeDrive", "HudWillyWrongAddress", "HudWillyContinue"));
        _log.LogInfo(
            $"where is willy: wrong address {AddressFormatter.Format(door.Address)}, " +
            $"friendship {_friendships.GetLevel(_elder.Name.BaseName)}");
    }

    // DROP OFF: the fee, the certificate and the completion stamp land in one
    // banked call (Unity's receipt-on-show order), then the page opens on the
    // numbers it was charged.
    private WhereIsWillyReceiptScreenViewModel? Complete()
    {
        if (_receipt is not { } receipt || _elder is null)
        {
            return null;
        }

        _wallet.AddBonus(receipt.FinderFee);
        string certificate = Strings.Format(
            "WillyCertificateName", receipt.Week, receipt.Year);
        _trunk.AddKeepsake(certificate, 1);
        _ledger.MarkCompleted(_game.GameNow);
        _saver.SaveDriver(_rivals.LastResult);
        _hud?.SetDestination(null);
        Reset();
        _log.LogInfo(
            $"where is willy paid: {receipt.FinderFee} CR, certificate '{certificate}', " +
            $"stamp {_ledger.LastCompletedDate}");
        return new WhereIsWillyReceiptScreenViewModel(
            receipt, _beat, new RelayCommand(Continue));
    }

    private WhereIsWillyReceipt BuildReceipt()
    {
        Resident elder = _elder!;
        PointOfInterest shop = _shop!;
        return WhereIsWillyReceiptBuilder.Build(
            _rules, elder, shop, _attempts, OwnedCarSpecs(), _shopCount,
            _displayWeek(), CalendarStamp.Default.Year,
            _friendships.GetLevel(elder.Name.BaseName));
    }

    // The catalogue rows behind the owned cars; a retired key still counts as a
    // car the player owns, so it contributes its spec when the catalogue knows
    // it and nothing when it does not.
    private List<CarSpec?> OwnedCarSpecs()
    {
        List<CarSpec?> specs = new(_garage.Cars.Count);
        foreach (OwnedCar car in _garage.Cars)
        {
            specs.Add(_garage.SpecOf(car) ?? _catalog.GetValueOrDefault(car.Key));
        }
        return specs;
    }

    private WhereIsWillyScreenViewModel CreateQuestPage() => new(
        _phase, _elder!, _home, _map.City,
        _phase == WhereIsWillyPhase.Searching ? Shops() : [],
        _missed, ChooseCoffeeShop, ChooseHome, Abandon, new RelayCommand(Back), _beat);

    private List<PointOfInterest> Shops() =>
        [.. WhereIsWillyCandidates.CoffeeShops(_directory.All)];

    // The elder's door as a quest marker: the name is the elder's first name,
    // because the HUD copy's {0} is the marker name.
    private PointOfInterest HomeMarker() =>
        new(HomeMarkerId, _elder?.Name.FirstName ?? string.Empty, _home, PoiTypes.Willy);

    private static HudDestinationCopy HomeCopy() =>
        new("HudWillyHomeDrive", "HudWillyHomeArrived", "HudWillyDropOff");

    private static bool IsCoffeeShop(PointOfInterest poi) =>
        string.Equals(poi.Type, PoiTypes.CoffeeShop, StringComparison.OrdinalIgnoreCase);

    private void Reset()
    {
        _phase = WhereIsWillyPhase.Idle;
        _shop = null;
        _elder = null;
        _home = default;
        _shopCount = 0;
        _attempts = 0;
        _missed = false;
        _receipt = null;
    }
}
