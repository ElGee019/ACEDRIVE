using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Prices and banks one finished job. The taxi receipt and the delivery run
/// apply Unity's effects in the same order — familiarity, brand km, wallet,
/// career counters, bank interest, zone expansion, standings, dirt, checkpoint
/// saves — so they live together here instead of as two 130-line methods in the
/// shell. State owners are injected; the page it lands on and the shared saves
/// come from <see cref="IJobSettlementHost"/>; the two values that belong to the
/// session (home country, wake-word brand) ride as parameters.
/// </summary>
public sealed class JobSettlement
{
    // Receipt fallbacks for an unknown catalogue row: the car's class/rarity
    // come from the owned car driven now, while an unowned car deliberately
    // reads 0 × 0 so it earns no XP. Street knowledge has no system here yet,
    // so every job settles at Unity's fully-known 100 %.
    internal const int FallbackCarClass = 1;
    internal const int FallbackCarRarity = 1;
    internal const int PlaceholderStreetKnowledgePct = 100;

    private readonly DriverWallet _wallet;
    private readonly Garage _garage;
    private readonly TaxiShift _shift;
    private readonly OperatingZone _zone;
    private readonly JobLedger _jobs;
    private readonly DailyStreak _streak;
    private readonly IClock _clock;
    private readonly FleetService _fleet;
    private readonly ICarRaritySource _rarity;
    private readonly IFriendshipStore _friends;
    private readonly IBrandMileageStore _brands;
    private readonly IPlayerTelemetry _online;
    private readonly IRivalsService _rivals;
    private readonly DeliveryCatalog _deliveryCatalog;
    private readonly DeliveryCareer _career;
    private readonly CityEconomy _city;
    private readonly DeliveryFareTable _deliveryFares;
    private readonly TaxiDriveMeter _meter;
    private readonly FareTable _fares;
    private readonly TaxiRules _taxiRules;
    private readonly VoiceBeat _beat;
    private readonly ITelemetryHud? _hud;
    private readonly ILog _log;

    public JobSettlement(
        DriverWallet wallet, Garage garage, TaxiShift shift, OperatingZone zone,
        JobLedger jobs, DailyStreak streak, IClock clock,
        FleetService fleet, ICarRaritySource rarity, IFriendshipStore friends,
        IBrandMileageStore brands, IPlayerTelemetry online, IRivalsService rivals,
        DeliveryCatalog deliveryCatalog, DeliveryCareer career, CityEconomy city,
        DeliveryFareTable deliveryFares, TaxiDriveMeter meter,
        FareTable fares, TaxiRules taxiRules,
        VoiceBeat beat, ITelemetryHud? hud, ILog log)
    {
        _wallet = wallet;
        _garage = garage;
        _shift = shift;
        _zone = zone;
        _jobs = jobs;
        _streak = streak;
        _clock = clock;
        _fleet = fleet;
        _rarity = rarity;
        _friends = friends;
        _brands = brands;
        _online = online;
        _rivals = rivals;
        _deliveryCatalog = deliveryCatalog;
        _career = career;
        _city = city;
        _deliveryFares = deliveryFares;
        _meter = meter;
        _fares = fares;
        _taxiRules = taxiRules;
        _beat = beat;
        _hud = hud;
        _log = log;
    }

    /// <summary>
    /// Prices the live meter, banks it to the wallet, the driver save and the
    /// shift, and shows the receipt. The odometer already banked the wear per
    /// poll, so the settle must not add the distance a second time.
    /// </summary>
    public void SettleTaxiRide(
        TaxiJobState job, string homeCountry, string carBrand, IJobSettlementHost host)
    {
        job.Phase = JobPhase.Complete;
        // The speed challenge's verdict (PORTPLAN 06): winning it lifts
        // cornering by 1, the way Unity's accepted challenge did before the
        // port made the gamble a success condition. The settle's own driver
        // save below banks it.
        if (ResolveSpeedChallenge(job))
        {
            _wallet.AddCornering(1.0);
            _log.LogInfo($"[Rating] speed challenge won: cornering {_wallet.Rating.Cornering:0.00}");
        }
        int capacity = _fleet.PassengerCapacity();
        // The car that actually ran the ride. An owned car supplies its real
        // class + global rarity and contributes its km. An unowned free-roam
        // car reads 0 × 0 (no XP) and suppresses every career write below;
        // only its fare still pays.
        CarSpec? xpCar = _fleet.XpCar();
        bool earnsProgress = !_fleet.IsDrivingUnownedCar();
        int carClass = FallbackCarClass;
        int carRarity = FallbackCarRarity;
        if (earnsProgress)
        {
            if (xpCar is { Class: > 0 } knownClass)
            {
                carClass = knownClass.Class;
            }
            if (xpCar is not null)
            {
                carRarity = _rarity.GetRarity(xpCar.Key);
            }
        }
        else
        {
            // An unowned car has no class/rarity in the career ledger, so the
            // receipt's own XP formula reads 0 × 0.
            carClass = 0;
            carRarity = 0;
        }
        string carKey = xpCar?.Key ?? string.Empty;
        int tripKm = TripKm(job.DistanceKm);
        // Unity keys familiarity off the base name (no companion suffix):
        // every completed ride is +1 for that resident, strangers included
        // (0 -> 1 on the first ride). The outcome rides the receipt so the
        // page renders it; transitions log, quiet rides do not.
        string baseName = job.Customer.BaseName;
        FriendshipOutcome friendship = _friends.RegisterRide(baseName);
        // Unity banks the trip km into the driven brand before pricing, so
        // the current ride already counts toward its own sponsorship rate.
        string brandName = _fleet.SponsorBrand(carBrand);
        double brandKm = _brands.RegisterRide(brandName, job.DistanceKm);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            job.DistanceKm, capacity, carClass,
            carRarity, PlaceholderStreetKnowledgePct,
            tipEligible: job.TipEligible || job.QuizAnsweredCorrectly,
            fines: job.SpeedChallengeFines, _fares, _wallet.Xp,
            new FriendshipResult(friendship.OwnerName, friendship.Level, friendship.TierIncreased),
            brandName, brandKm, carKey,
            // The starter bonus decays with the operating zone and the car
            // count (PORTPLAN 47, Unity ProvincialStarterBonus).
            zoneKm: _zone.Km, ownedCars: _garage.Cars.Count);
        if (friendship.TierIncreased)
        {
            _log.LogInfo($"friendship tier up: {friendship.OwnerName} -> {friendship.Level}");
        }
        job.Fare = receipt.BaseTotal;
        job.BrandBonus = receipt.BrandBonus;
        job.StarterBonus = receipt.StarterBonus;
        job.Tip = receipt.Tip;
        job.Fine = receipt.FineDeduction;
        _wallet.AddReceipt(receipt);
        // An unowned car banks the fare but no career progress: no global car
        // km, country km, zone job or XP standings. The interest still
        // compounds — it belongs to the bank, not to the car.
        if (earnsProgress)
        {
            if (xpCar is not null && tripKm > 0)
            {
                _rarity.RecordKm(xpCar.Key, tripKm);
            }
            if (tripKm > 0)
            {
                _online.IncrementCountryKm(homeCountry, tripKm);
            }
            // Zone tick (PORTPLAN 58, Unity expansionJobsCompleted++): every
            // settled taxi ride counts; the interest lands in the bank before
            // the expansion street joins (Unity orders it the same way —
            // counter, interest, street registration at the receipt CONTINUE).
            // The lifetime total (Unity jobsCompleted) rides the same hook and
            // gates the job-opened businesses.
            _zone.RegisterJob();
            _jobs.Register();
        }
        ApplyBankInterest();
        if (earnsProgress)
        {
            host.OnProgressBanked();
            host.RefreshStandings();
        }
        else
        {
            // The settle still banks cash and the shift; only the XP push is
            // skipped. The last known board rides along unchanged.
            host.SaveDriver(_rivals.LastResult);
        }
        _shift.RecordCompleted(job, _clock.UtcNow);
        // The dropoff restamps the idle clock; bank it so a process death
        // right after the receipt keeps the shift's 30 minutes honest.
        host.SaveShift();
        // Dirt tick (PORTPLAN 33, Unity TaxiJobLifecycleHandler's dropoff
        // block): every completed ride is +1 on the car driven now.
        _fleet.AddDirt();
        // Checkpoint bank: a paid job is progress the player can see, so the
        // moment goes to disk now instead of waiting for the throttle. The
        // garage rides along so the dirt tick is not lost with the process.
        host.SaveClock();
        host.SaveGarage();
        _log.LogInfo($"job dropoff: {job.PickupName} -> {job.DropoffName} " +
                  $"dist={receipt.DistanceKm:0.00}km car={(carKey.Length > 0 ? carKey : "unowned")} " +
                  $"class={carClass} rarity={carRarity} xp={receipt.XpEarned:0.00} " +
                  $"brand={receipt.BrandName} bonus={receipt.BrandBonus:0.00} " +
                  $"starter={receipt.StarterBonus:0.00} " +
                  $"wallet={_wallet.Cash:0.00} shift={_shift.JobsDone} jobs / {_shift.Earned:0.00}");
        host.ShowReceipt(new TaxiReceiptScreenViewModel(
            receipt, job.Customer.Full, _wallet, _shift, _beat,
            new RelayCommand(host.GoToControlPage), _rivals, _rarity));
        host.RefreshJob();
    }

    /// <summary>
    /// The last store is served: the measured store-leg distance prices the
    /// receipt, the sum of every stop pays out exactly like Unity's per-store
    /// ApplyStoreDeliveryEffects, and the run banks once. Every ordered shelf
    /// is filled even if a stop was skipped for a missing address.
    /// </summary>
    public void SettleDeliveryRun(
        DeliveryRun run, string homeCountry, IJobSettlementHost host)
    {
        host.ClearDeliveryRun();
        double distanceKm = _meter.DistanceKm;
        _meter.Reset();
        _ = _city.Restock(run.StoreKeys);

        // Unity ApplyStoreDeliveryEffects runs per stop; the sum receipt
        // totals it: each store credits its producing hub's ramp, distinct
        // owners collect. A store the catalog no longer lists (content
        // changed mid-run) earns nothing instead of crashing the settle.
        List<string> owners = [];
        int credPEarned = 0;
        foreach (string key in run.StoreKeys)
        {
            if (_deliveryCatalog.Find(key) is not { } store)
            {
                continue;
            }
            DeliveryEarning earning = _career.RegisterDelivery(store.Category);
            credPEarned += earning.CredPEarned;
            if (earning.OwnerName.Length > 0 && !owners.Contains(earning.OwnerName))
            {
                owners.Add(earning.OwnerName);
            }
        }
        // Unity keys the run's familiarity on the producing hub's owner
        // once the row completes. One sourced hub rides the receipt exactly
        // like a taxi passenger; mixed-hub runs still credit every owner
        // (progress never stalls) but hide the friendship block until the
        // per-stop receipts land with the map's store→hub routing.
        FriendshipResult? friendship = null;
        if (owners.Count == 1)
        {
            FriendshipOutcome outcome = _friends.RegisterRide(owners[0]);
            friendship = new FriendshipResult(outcome.OwnerName, outcome.Level, outcome.TierIncreased);
            if (outcome.TierIncreased)
            {
                _log.LogInfo($"friendship tier up: {outcome.OwnerName} -> {outcome.Level}");
            }
        }
        else
        {
            foreach (string owner in owners)
            {
                _ = _friends.RegisterRide(owner);
            }
        }
        // An unowned free-roam car pays the run's money but earns no career
        // progress, exactly like the taxi settle: no XP, country km, zone job
        // or XP standings.
        bool earnsProgress = !_fleet.IsDrivingUnownedCar();
        DeliveryReceipt receipt = DeliveryReceiptBuilder.Build(
            distanceKm, run.PaidForGoods, run.ReceivedForGoods,
            _deliveryFares, PlaceholderStreetKnowledgePct, _wallet.Xp,
            friendship, credPEarned, _career.CredP, awardsXp: earnsProgress);
        _wallet.AddDelivery(receipt);
        // Delivery's km feed: the owned car contributes to the global rarity
        // board and the country aggregate, exactly like the taxi settle. The
        // delivery XP itself stays class/rarity-free (Unity parity).
        CarSpec? deliveryCar = _fleet.XpCar();
        int deliveryKm = TripKm(distanceKm);
        if (earnsProgress)
        {
            if (deliveryCar is not null && deliveryKm > 0)
            {
                _rarity.RecordKm(deliveryCar.Key, deliveryKm);
            }
            if (deliveryKm > 0)
            {
                _online.IncrementCountryKm(homeCountry, deliveryKm);
            }
            // Zone tick, delivery twin (Unity DeliveryJobHandler's settle): the
            // counters, interest and expansion flow exactly like the taxi hook.
            _zone.RegisterJob();
            _jobs.Register();
        }
        ApplyBankInterest();
        if (earnsProgress)
        {
            host.OnProgressBanked();
            host.RefreshStandings();
        }
        else
        {
            host.SaveDriver(_rivals.LastResult);
        }
        // Dirt tick, delivery twin (PORTPLAN 33, Unity DeliveryJobHandler's
        // store arrival): a completed run is +1 on the car driven now.
        _fleet.AddDirt();
        // The cached board must show the filled shelves and any store the
        // expansion just placed before CONTINUE hands back to it.
        host.RefreshDeliveryBoard();
        // Checkpoint bank, same rule as the taxi dropoff: the paid run is
        // progress the player can see, so it goes to disk now.
        host.SaveClock();
        host.SaveGarage();
        _log.LogInfo($"delivery settled: {run.StoreKeys.Count} stores " +
            $"dist={receipt.DistanceKm:0.00}km " +
            $"paid={receipt.PaidForGoods:0.00} received={receipt.ReceivedForGoods:0.00} " +
            $"fare={receipt.DistanceCharge:0.00} earned={receipt.TotalEarned:0.00} " +
            $"xp={receipt.XpEarned:0.00} credP=+{receipt.CredPEarned}/{_career.CredP:0} " +
            $"wallet={_wallet.Cash:0.00}");
        host.ShowReceipt(new DeliveryReceiptScreenViewModel(
            receipt, _wallet, _beat,
            new RelayCommand(host.GoToDeliveryBoard), _rivals));
    }

    // Unity's whole-km trip unit (Mathf.RoundToInt with a 1 km floor): the
    // country/car aggregates never receive a sub-kilometre or negative sample.
    private static int TripKm(double distanceKm) =>
        distanceKm > 0.0 && double.IsFinite(distanceKm)
            ? Math.Max(1, (int)Math.Round(distanceKm))
            : 0;

    // Bank interest at the two settle sites (PORTPLAN 48, Unity
    // BankInterestHelper.ApplyForCompletion): credited INTO the bank after
    // the payday so it compounds, never on the car-haul settle (no hook
    // there — Unity parity). A fresh driver already answers rate 1 (the
    // home street × day one), so the saldo grows from the first job.
    private void ApplyBankInterest()
    {
        int rate = BankRules.Rate(_zone.StreetIds.Count, _streak.DaysInARow);
        if (rate <= 0)
        {
            // Unity skips the event on a zero rate: nothing to credit, no log.
            return;
        }
        double interest = BankRules.ComputeInterest(_wallet.BankBalance, _zone.StreetIds.Count, _streak.DaysInARow);
        _wallet.ApplyInterest(interest);
        _log.LogInfo($"[Bank] interest +{interest:0.00} CR " +
            $"(rate {rate}, bank {_wallet.BankBalance:0.00})");
    }

    // The deadline only removes the ordinary tip; the fare and XP settle as
    // usual. The buffer in the estimate already covers the stop at the door,
    // so the HUD's TOO LATE line and this check agree. Returns true when the
    // challenge was agreed AND beaten, which is the cornering award's gate
    // (PORTPLAN 06): an untimed challenge has nothing to succeed at.
    private bool ResolveSpeedChallenge(TaxiJobState job)
    {
        if (!job.SpeedChallengeAgreed
            || job.SpeedChallengeDeadline is not { } deadline)
        {
            return false;
        }

        if (_clock.UtcNow <= deadline)
        {
            return true;
        }

        job.TipEligible = false;
        _hud?.ShowNotice(
            Strings.Get("HudSpeedChallengeLate"),
            TimeSpan.FromSeconds(_taxiRules.NoticeSeconds));
        _log.LogInfo(
            $"speed challenge late: {(_clock.UtcNow - deadline).TotalSeconds:0}s " +
            $"past the deadline; tip dropped");
        return false;
    }
}
