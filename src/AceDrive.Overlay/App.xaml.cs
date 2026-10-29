using System.IO;
using System.Windows;
using System.Windows.Threading;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;
using AceDrive.Overlay.Views;

namespace AceDrive.Overlay;

/// <summary>
/// Sole composition root (DIP): the only place that news up services.
/// Views and ViewModels never construct their own dependencies.
/// Owns the process-wide logger, hence IDisposable (CA1001).
/// </summary>
public partial class App : Application, IDisposable
{
    private FileLogger? _log;
    private FirebaseConnection? _firebase;
    private FirestoreValuationBoard? _valuationBoard;
    private bool _disposed;

    // TEMPORARY (testing): keeps every calendar event playable on any day and
    // hour and lets the one-shot events (the missing-person run, the market
    // valuation) be replayed. Flip to false to restore the shipping gates; the
    // flag and the three arguments it feeds are deleted once the playtest ends.
    private const bool IgnoreEventGatesForTesting = true;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        _log = new FileLogger();
        _log.LogInfo("AceDrive starting...");
        // The car-thumbnail loader: the converter declared in App.xaml gets
        // its file boundary here, so no view ever decodes a file itself.
        if (Resources["ThumbnailImage"] is ImagePathConverter thumbnails)
        {
            thumbnails.Source = new ThumbnailImageSource();
        }
        // Embedded menu artwork rides the same seam, with the WIC-safe decode
        // that keeps a WebP-less machine on the plain backdrop.
        if (Resources["PackImage"] is PackImageConverter artwork)
        {
            artwork.Source = new PackImageSource();
        }
        // WHY the saved language first, before any Strings.Get: every screen's
        // copy resolves through the UI culture, so the language the player
        // picked on the first-run page must already be in place at boot. A
        // fresh install has no file yet, so it opens English until answered.
        var preferencesStore = new JsonPreferencesStore(_log);
        var preferences = preferencesStore.Load();
        var culture = new CultureService();
        culture.Apply(preferences.Language);
        var fares = new JsonFareStore(_log).Load();
        var todos = new JsonTodoStore(_log).Load();
        // The Cento Miglia tier table (PORTPLAN 49): shipped content, read once
        // at startup like the fares so no page touches a file.
        IReadOnlyList<CentoTier> centoTiers = new JsonCentoTierStore(_log).Load();
        // The Cento Miglia race balance (PORTPLAN 49): the placement ladder, the
        // Star of the Show curve and the generated item names, shipped as
        // content like the tier table so no rule class carries a number.
        CentoRaceRules centoRaceRules = new JsonCentoRaceRulesStore(_log).Load();
        // The weekly missing-person run's balance (ToWhereIsWilly.md §3.2): the
        // finder's fee, its multipliers and the 100 CR floor, shipped as
        // content like the fares so no rule class carries a number.
        WhereIsWillyRules willyRules = new JsonWhereIsWillyRules(_log).Load();
        // The placeholder address map grounds the HUD, the onboarding home and
        // the taxi job doors; load it before the proposal draw.
        var addressMap = new JsonAddressMapStore(_log).Load();
        // WHY TickCount: like the road-test ledger, the draws want their own
        // seeded instances (never the shared global) and tests pin the seed.
        // Resident names and the address/flip draw are independent streams,
        // so their seeds are offset to keep the first resident from tracking
        // the first proposal.
        int taxiSeed = Environment.TickCount;
        // One name-pool read for the residents and the generated business
        // owners; the two draw from independent seeded streams.
        NamePoolTable namePools = new JsonNamePoolStore(_log).Load();
        var residents = new ResidentBook(
            new JsonResidentStore(_log),
            new ProceduralNameGenerator(namePools, taxiSeed),
            _log);
        var poiNames = new ProceduralNameGenerator(namePools, taxiSeed + 4);
        var content = new JsonOnboardingContent(_log);
        // WHY touch Content once at startup: the same eager first read (and
        // "content:" log line) the old Load() call gave, so a missing/corrupt
        // Data dir still degrades at boot; from here the source re-reads on
        // its own whenever the files change — no restart for alias edits.
        _ = content.Content;
        // Career Start Screen 1 (opening): the saved profile, the placeholder
        // address map, and the Steam display name that fills a first run.
        // The profile wins on later launches so edits survive a restart.
        var profileStore = new JsonProfileStore(_log);
        // The story POIs (PORTPLAN 5/24/27): names from Data/pois.json. The
        // flow banks each random house on its first draw and reuses it, so the
        // generator only seeds the doors that do not exist yet. WHY TickCount:
        // its own seeded instance, never the shared global; tests pin the seed.
        // WHY the whole map and not the zone: the introduction's errand and the
        // friends' doors are scripted onboarding content, not zone draws
        // (TOZONES §4.7), and their tests pin the distribution.
        var poiTable = new JsonPoiStore(_log).Load();
        var poiGenerator = new PoiGenerator(addressMap, poiTable, Environment.TickCount);
        // The business POIs (PORTPLAN 24): no door exists on a new career. The
        // spawner opens the car wash, bank, dealer, mechanic and insurance on
        // free houses as the operating zone and the settled-job count cross
        // Unity's thresholds; the host checks it at boot and on every settle.
        // Separate seeded stream so a spawn never reshuffles a resident name.
        var poiSpawner = new PoiSpawner(poiTable, poiNames, taxiSeed + 4);
        // The addresses page (PORTPLAN 24/27): every discovered door lands in
        // one banked directory. Story doors arrive first, then the spawned
        // businesses; both survive a restart.
        var poiStore = new JsonPoiDirectoryStore(_log);
        IReadOnlyList<PointOfInterest> savedPois = poiStore.Load();
        var keptPois = new List<PointOfInterest>(savedPois.Count);
        foreach (PointOfInterest poi in savedPois)
        {
            if (!IsRetiredAtStartDoor(poi, poiTable))
            {
                keptPois.Add(poi);
            }
        }
        var poiDirectory = new PoiDirectory(keptPois);
        if (keptPois.Count != savedPois.Count)
        {
            // The at-start build drew fixed doors named after their definition
            // id ("bank", "dealer", …). The business doors now open from the
            // street/job thresholds, so those fixed rows are dropped once; the
            // generated ids ("bank_0") and every story door stay. The boot
            // growth below re-opens whatever the saved progress actually earned.
            poiStore.Save(poiDirectory.All);
            _log.LogInfo(
                $"[Poi] dropped {savedPois.Count - keptPois.Count} retired at-start door(s)");
        }
        // Shop content (PORTPLAN items 24/26): the item database + per-store
        // layout. A missing/corrupt file degrades to the built-in catalog.
        var storeCatalog = new JsonStoreItemStore(_log).Load();
        var stores = new StoreNetwork(storeCatalog, poiTable);
        var playerName = new SteamPersonaNameSource(_log).TryReadName();
        // The delivery board: the balance table plus every real shop door in
        // the persisted directory (Max's story grocery and the stores each
        // owned hub backfilled). The POI directory is the persisted identity,
        // so the board row and the shop interior rebuild the same store from
        // the same catalogue; the town then restores its shelf cell.
        var deliveryCatalog = new JsonDeliveryCatalog(_log).Load()
            .WithStores(PlacedStore.Rows(poiDirectory.All, storeCatalog));
        // The delivery distance-charge table rides beside the taxi fares:
        // one file each, so balance edits never collide.
        var deliveryFares = new JsonFareStore(_log).LoadDelivery();
        // The introduction's banked visit: a new driver starts with nothing and
        // only Max's first store visit after the introduction hands over the
        // intro CredP (Unity firstSceneComplete + hasSeenMaxJobIntro, PORTPLAN
        // 5/51). The career itself resumes from the driver save below; this flag
        // only seeds a save written before the career persisted.
        var onboardingStore = new JsonOnboardingStore(_log);
        OnboardingState onboardingProgress = onboardingStore.Load();
        bool introGiftClaimed = onboardingProgress.MaxDeliveryIntroSeen;

        // The town (PORTPLAN 25): each store starts at the rules' citizen
        // value, its people drift with how full the shelves are and demand
        // empties them once a hub is owned. The saved cells are resumed so a
        // restart reopens the same city instead of re-seeding it.
        var cityStore = new JsonCityStore(_log);
        var city = new CityEconomy(
            deliveryCatalog.Stores, new JsonCityRules(_log).Load(), cityStore.Load());

        // The appetite (PORTPLAN 26, Unity GameTimeConfig + PlayerDefaults):
        // the meter resumes the last reading, so a restart does not re-feed
        // the driver.
        var hungerStore = new JsonHungerStateStore(_log);
        var hunger = new Hunger(new JsonHungerRules(_log).Load(), hungerStore.Load());

        // WHY Saturday 08:00: mirrors the Unity fresh save (PlayerDefaults:
        // gameHour 8, dayOfWeek 7) so a new overlay opens on the same
        // morning the Unity game does; weeks count from CalendarStamp.
        var wall = new SystemClock();
        // The shared voice beat: one clock-driven timer for every screen's
        // acknowledgment glow and its navigation wait (VoiceBeat).
        var beat = new VoiceBeat(wall);
        // WHY TickCount: the ledger needs its own seeded instance (never the
        // shared global) and only a fresh draw uses it; tests pin the seed.
        // A saved pick beats the draw, so the road-test car is the same car
        // in every session until that car's test is completed (PORTPLAN 49).
        var roadTestStore = new JsonRoadTestStore(_log);
        var cars = new JsonCarStore(_log).Load();
        var roadTests = new RoadTestLedger(
            cars, Environment.TickCount, roadTestStore.Load());
        // The garage (PORTPLAN 30): the owned cars + the car driven now,
        // resumed from its own file beside the driver. An empty garage on a
        // finished onboarding (every pre-garage save) is seeded with grandma's
        // car by the VM, which owns that onboarding state.
        var garageStore = new JsonGarageStore(_log);
        // The condition tables (PORTPLAN items 31/32): the part intervals/prices
        // and the engine bands every owned car reads, plus the breakdown
        // envelope the trip-start roll uses.
        MaintenanceRules maintenanceRules = new JsonMaintenanceRules(_log).Load();
        // The filth rules (PORTPLAN 33, Unity CarWashService) and the condition
        // tables ride into the fleet: every owned car clamps its dirt and reads
        // its part/engine rules from the same loaded data. The seed drives the
        // engine inspection minutes (EngineHealth).
        var garage = new Garage(
            cars, garageStore.Load(), new JsonFilthRules(_log).Load(),
            maintenanceRules, Environment.TickCount);
        // The insurance table (PORTPLAN 34): the nightly premium rate and the
        // broker page's daily-cost math.
        var insuranceRules = new JsonInsuranceRules(_log).Load();
        // Resume the game moment: a missing or corrupt clock file means a
        // fresh Saturday morning on week 1 (the store never throws).
        var clockStore = new JsonGameClockStore(_log);
        GameClockState? saved = clockStore.Load();
        // The driver (PORTPLAN 11/43): the cash wallet + XP total resume
        // from the last session, so money earned on dropoff survives a
        // restart. A missing or corrupt file means a fresh driver.
        var driverStore = new JsonDriverStore(_log);
        DriverState? savedDriver = driverStore.Load();
        var driver = new DriverWallet(savedDriver);
        // The delivery career (PORTPLAN 18/51): the CredP balance, the hubs
        // bought and each hub's ramp count resume from the driver save, so a
        // restart reopens the career instead of losing it. A save written
        // before the career persisted has no career block: its hub doors are
        // still in the address directory, so the ownership is recovered from
        // them once, and only a genuinely new career falls back to the banked
        // intro visit's gift.
        var hubCatalog = new JsonHubCatalog(_log).Load();
        var deliveryCareer = savedDriver?.Career is { } savedCareer
            ? new DeliveryCareer(hubCatalog, savedCareer)
            : new DeliveryCareer(hubCatalog,
                DeliveryCareerRecovery.Recover(hubCatalog, poiDirectory.All)
                ?? new DeliveryCareerState(introGiftClaimed ? deliveryCatalog.StartCredP : 0));
        // The lifetime settled-job total (PORTPLAN 24, Unity jobsCompleted):
        // the dealer and mechanic POIs gate on it, so it must survive a
        // restart beside the cash it was earned with.
        var jobs = new JobLedger(savedDriver);
        // The ToDo career readings (PORTPLAN 46): the manual-destination count
        // and the one-time business visits ride the driver file, so the four
        // tips measure a real career instead of a placeholder.
        var pins = new DestinationPins(savedDriver);
        var visits = new VisitLedger(savedDriver);
        // The weekly market valuation (PORTPLAN 49): the preloaded venue, the
        // in-flight leg, the week's entry and the settled uid set resume from
        // the driver file, so a restart reopens the same showcase week.
        var valuation = new ValuationLedger(savedDriver);
        // The missing-person run's persisted half (ToWhereIsWilly.md §2.9): the
        // game date its receipt last stamped, so a restart closes the row for
        // the rest of that Wednesday.
        var willyLedger = new WhereIsWillyLedger(savedDriver);
        var todoStore = new JsonTodoProgressStore(_log);
        // The open taxi shift resumes with its wall-clock stamps (PORTPLAN 12):
        // the 30-minute idle rule keeps running while the app and EVO are
        // closed, so a restart inside the window continues the same shift and
        // a later one opens on a closed shift.
        var shiftStore = new JsonShiftStore(_log);
        var shift = new TaxiShift(shiftStore.Load());
        // The best single shift (PORTPLAN 12) and its last world rank resume
        // beside the wallet; the online layer pushes the record on shift end.
        var shifts = new ShiftRecord(savedDriver);
        shifts.RestoreStandings(savedDriver?.ShiftRank ?? 0, savedDriver?.ShiftTotal ?? 0);
        // The trunk rides the driver state (Unity PlayerData.trunkItems), so
        // bought goods and the spent cash survive a restart together.
        var trunk = new Trunk(savedDriver?.Trunk);
        // The login streak (PORTPLAN 47): recomputed from the saved dates at
        // every boot, so the counter is always fresh against today.
        var streak = new DailyStreak(wall, savedDriver);
        // The operating zone (PORTPLAN 58): the street-growth counters resume
        // from the last session; the threshold curve re-draws its seeded
        // lottery per launch, Unity GetNextExpansionThreshold parity.
        // The profile's home street leads the discovered order, so the first
        // expansion claims the other base street before the fictional pool.
        // The placeholder's base streets supply the interim zone length: a
        // 0 km zone would pin the business starter bonus at 100% (47/58).
        var zone = new OperatingZone(
            savedDriver, Environment.TickCount, addressMap,
            homeStreetId: profileStore.Load()?.StreetId,
            initialKm: AddressBook.MapLengthKm(addressMap));
        // The taxi proposal draw reads the zone's own map, never the whole
        // address file: a fictional street the zone has not discovered yet must
        // never be offered as a destination (TOZONES §4.7). Hence it is built
        // after the zone.
        var taxiProposals = new TaxiJobProposer(zone, residents, taxiSeed + 1);
        // XP world standings (PORTPLAN 60): the live Firestore board when a
        // config exists, otherwise the offline null-object. The saved rank
        // snapshot restores either way (Unity's LoadFromPlayerData), so the
        // first receipt already knows the last seen standing.
        IRivalsService rivals;
        ICarRaritySource rarity;
        IPlayerTelemetry telemetry;
        IShiftStandings shiftStandings;
        IZoneStandings zoneStandings;
        IPlayerNameRegistry playerNames;
        IValuationBoard valuationBoard;
        IRoadTestStandings roadTestStandings;
        FirebaseOptions? firebaseOptions = FirebaseOptions.TryLoad(_log);
        if (firebaseOptions is not null)
        {
            _firebase = new FirebaseConnection(firebaseOptions, _log);
            _firebase.Start();
            var onlinePlayers = new FirestorePlayers(_firebase.Database, wall, _log);
            var liveRivals = new FirestoreRivals(
                onlinePlayers, () => _firebase?.Session.Uid,
                () => profileStore.Load()?.DisplayName,
                _log, _firebase.StopToken);
            liveRivals.RestoreSnapshot(savedDriver);
            liveRivals.Start();
            liveRivals.Refresh(driver.Xp);

            // The catalogue size bounds the rarity multiplier (half the car
            // list), so the cap is derived from the shipped cars, not from
            // how many cars the global board happens to hold today.
            var liveRarity = new FirestoreCarRarity(
                new FirestoreCars(_firebase.Database, wall), cars.Count,
                _log, _firebase.StopToken);
            liveRarity.Start();

            var liveShiftBoard = new FirestoreShiftStandings(
                onlinePlayers, () => _firebase?.Session.Uid,
                () => profileStore.Load()?.DisplayName,
                _log, _firebase.StopToken);
            liveShiftBoard.RestoreSnapshot(savedDriver);
            liveShiftBoard.Start();

            // The longest-operating-zone board (PORTPLAN 58): the expansion
            // page's rank, on the same players collection and stop token as the
            // other live boards. Its own write is the settle's
            // PushZoneKm, so the board class only reads and ranks.
            var liveZoneBoard = new FirestoreZoneStandings(
                onlinePlayers, () => _firebase?.Session.Uid,
                () => profileStore.Load()?.DisplayName,
                _log, _firebase.StopToken);

            rivals = liveRivals;
            rarity = liveRarity;
            shiftStandings = liveShiftBoard;
            zoneStandings = liveZoneBoard;
            // WHY UidAsync: the boot poll can end a shift that timed out while
            // the game was closed before anonymous auth has finished, and the
            // shift push has no later retry of its own.
            telemetry = new FirestorePlayerTelemetry(
                onlinePlayers,
                new FirestoreCountries(_firebase.Database, wall),
                _firebase.UidAsync, _log, _firebase.StopToken);
            playerNames = new FirestorePlayerNameRegistry(
                _firebase.Database, _firebase.UidAsync, wall, _log);

            // The weekly valuation list (PORTPLAN 49): the same anonymous uid
            // and the same stop token as the other live boards. Its watcher is
            // a background re-read, never part of the 10 Hz poll.
            _valuationBoard = new FirestoreValuationBoard(
                new FirestoreValuationEntries(
                    _firebase.Database, _firebase.UidAsync, wall, _log),
                () => _firebase?.Session.Uid, _log, _firebase.StopToken);
            valuationBoard = _valuationBoard;
            // The per-car road-test boards (PORTPLAN 49): one doc per (uid, car)
            // holding the best 0-60 run, read once after each test and pushed
            // best-guarded. No watcher: a board is only looked at on the result
            // page.
            roadTestStandings = new FirestoreRoadTestStandings(
                _firebase.Database, _firebase.UidAsync, () => _firebase?.Session.Uid,
                wall, _log);
        }
        else
        {
            var offline = new OfflineRivals();
            offline.RestoreSnapshot(savedDriver);
            rivals = offline;
            rarity = new OfflineCarRarity();
            telemetry = new OfflinePlayerTelemetry();
            var offlineShiftBoard = new OfflineShiftStandings();
            offlineShiftBoard.RestoreSnapshot(savedDriver);
            shiftStandings = offlineShiftBoard;
            // No backend: the expansion page hides its standings block.
            zoneStandings = new OfflineZoneStandings();
            playerNames = new OfflinePlayerNameRegistry();
            // No backend: the list is empty and the house entry is the only
            // opponent, exactly like Unity's unconfigured build.
            valuationBoard = new OfflineValuationBoard();
            // No backend: the road-test page keeps the row the player just
            // drove, like Unity's local board when the list load fails.
            roadTestStandings = new OfflineRoadTestStandings();
        }
        // The zone board loads in the background from boot, so the first
        // expansion page already has a rank (the offline null-object no-ops).
        zoneStandings.Start();
        // Familiarity (PORTPLAN 45): one persisted level per resident, so a
        // restart reopens the same friendships instead of resetting them.
        var friendships = new JsonFriendshipStore(_log);
        // Brand mileage (PORTPLAN 35): lifetime km per driven brand behind
        // the receipt's sponsorship rate, resumed across sessions.
        var brands = new JsonBrandMileageStore(_log);
        // The typed POI handlers (PORTPLAN item 24) are assembled below, once
        // the missing-person flow exists: its marker handler joins the list
        // first so a quest marker never falls through to a business interior.
        var taxiRules = new JsonTaxiRulesStore(_log).Load();
        // Customer request content (PORTPLAN item 11): suffix lines, quiz
        // categories and tier chances. Its own seeded stream, offset from the
        // proposal/resident draws so a request roll never reshuffles them.
        var requestTable = new JsonCustomerRequestStore(_log).Load();
        var requests = new CustomerRequestRoller(cars, requestTable, taxiSeed + 2);
        // Speed-challenge patrols (Phase C): one seeded plan per accepted job,
        // offset from the other draws so it never reshuffles them. WHY the
        // whole map and not the zone: patrols are road events around the
        // player's own position, not destinations inside it (TOZONES §4.7).
        var policeRules = new JsonPoliceRulesStore(_log).Load();
        var police = new PolicePatrolPlanner(addressMap, policeRules, taxiSeed + 3);
        var hud = new HudViewModel(addressMap, preferences, beat, wall, taxiRules);
        // RESET GAME (settings): wipe every career file, then replace the
        // process so no in-memory store survives into the fresh introduction.
        var saveResets = new FileSaveDataResetter(_log);
        var restarter = new ProcessAppRestarter(_log);
        var game = new GameClock(
            wall, saved?.GameNow ?? new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc));
        var carBrands = new CarLogWatcher(_log);
        // The fleet and the shared state bank are built before the shell:
        // neither may depend on it, and the poll plus both settles take these
        // instances from here (AGENTS.md: this is the only composition point).
        var fleet = new FleetService(
            garage, cars, game, driver, insuranceRules, _log, carBrands, content);
        var meter = new TaxiDriveMeter(taxiRules.MaxMeterSpeedKmh);
        // The live board (a shop that connects while the overlay runs appends a
        // row) and the one instance the board page, the run planner and the
        // settle all read.
        var deliveryBoard = new DeliveryBoard(deliveryCatalog);
        // Prices and banks one finished job; the poll feeds the same meter, so
        // the instance is shared rather than owned by either side.
        var settlement = new JobSettlement(
            driver, garage, shift, zone, jobs, streak, wall,
            fleet, rarity, friendships, brands, telemetry, rivals,
            deliveryBoard, deliveryCareer, city, deliveryFares, meter,
            fares, taxiRules, beat, hud, _log);
        var saver = new GameStateSaver(
            wall, game, clockStore, driverStore, garageStore, cityStore,
            hungerStore, roadTestStore, shiftStore, driver, streak, zone, trunk,
            garage, city, hunger, roadTests, shift, shifts, jobs, pins, visits,
            valuation, willyLedger, deliveryCareer);
        // The panel, its page and the activity stamp: view state with an owner
        // the view binds through the shell, not state the coordinator holds.
        var shell = new OverlayShell();
        var shops = new ShopInteriors(
            driver, garage, game, insuranceRules, cars, saver, visits, beat, _log);
        int startWeek = saved?.Week ?? CalendarStamp.Default.Week;
        // The session's calendar: the day flip, the week it rolls and the idle
        // shift timeout with its ended page. The pages and the dealer's floor
        // read the week from here, so no mirror of it lives anywhere else.
        var dayCycle = new DayCycle(
            game, shift, shifts, shiftStandings, telemetry, profileStore,
            rivals, saver, beat, startWeek, _log);
        // The weekly missing-person run (ToWhereIsWilly.md): its own flow owns
        // the Wednesday draw, the search, the drive home and the receipt, so
        // the coordinator only forwards the calendar tap, the poll and the one
        // ENTER the HUD raises. Draws get their own seeded stream.
        var willyFlow = new WhereIsWillyFlow(
            zone, game, poiGenerator, poiDirectory, residents, garage, cars,
            willyRules, willyLedger, friendships, trunk, driver, hud, saver,
            rivals, beat, () => dayCycle.DisplayWeek, new Random(taxiSeed + 9), _log,
            ignoreEventGates: IgnoreEventGatesForTesting);
        // The road test (PORTPLAN 49): its own flow owns the drive to La Strada,
        // the 0-60 leg, the photo mission and the receipt page, so the
        // coordinator only forwards the calendar tap, the poll and the
        // companion's PHOTOSHOOT tap. Its reaction hold draws on its own seeded
        // stream, offset from the other launch draws. The audience reads the
        // same city and friendship stores the job settles do, and the settle
        // banks into the same wallet and saver.
        var roadTestAudience = new RoadTestAudienceSource(
            city, deliveryBoard, friendships);
        // The issue's balance (forfeit, magazine divisor, commission) is content:
        // Data/road_test.json, with Unity's values as the fallback.
        RoadTestAudienceRules roadTestAudienceRules =
            new JsonRoadTestRulesStore(_log).Load();
        var roadTestSettlement = new RoadTestSettlement(
            driver, roadTests, roadTestAudience, roadTestAudienceRules,
            saver, rivals, beat, _log);
        var roadTestFlow = new RoadTestFlow(
            onboardingStore, game, wall, roadTests, hud, roadTestStandings,
            profileStore, roadTestSettlement, cars, addressMap, beat,
            () => carBrands.CurrentIdentity, new Random(taxiSeed + 10), _log,
            ignoreEventGates: IgnoreEventGatesForTesting);
        // The quest's marker handler joins the typed arrival list first, so a
        // Willy marker can never fall through to a business interior.
        IPoiArrivalHandler[] poiHandlers =
        [
            new WhereIsWillyPoiHandler(willyFlow),
            new RoadTestPoiHandler(roadTestFlow),
            new StorePoiHandler(stores, trunk, driver, garage, friendships, beat),
        ];
        // The session-start catch-up (PORTPLAN items 30/47): the starter car a
        // pre-garage save never created, and today's login bonus. It owns the
        // streak counter and the skill-gain stream, so the coordinator does not
        // see either; the display week it stamps the seeded car with arrives as
        // a value at the call site (DayCycle owns it).
        var catchUp = new DriverCatchUp(
            onboardingStore, garage, streak, driver,
            // The streak's random skill gains (PORTPLAN 06/47): its own seeded
            // stream, offset from the taxi seed like the other draws.
            new Random(taxiSeed + 5), saver, rivals, _log);
        // The zone growth the settle triggers (PORTPLAN 58): the introduction
        // gate, the interim street length and the world push. The zone state
        // itself is shared with the saver, the settle and the world, so it
        // stays a service; this collaborator only decides when it grows.
        var zoneGrowth = new ZoneGrowth(
            zone, addressMap, onboardingStore, profileStore, telemetry, _log);
        // The readout formatter the poll applies: the painted strings and their
        // change caches, so no formatting runs on the hot path.
        var readouts = new TelemetryReadouts();
        // The game-clock world (PORTPLAN items 25/26/34): the town's drift, the
        // appetite and the nightly premium. One collaborator owns the three
        // rules so the poll only asks whether the town moved; the introduction
        // gate arrives as the coordinator's live value.
        var world = new WorldTicks(
            game, city, hunger, fleet, deliveryBoard, deliveryCareer, saver, rivals, _log);
        // The POI world and its arrival router: the store handler rides in from
        // here, the business interiors are appended by the world. The world
        // attaches to the coordinator as its host inside the VM's constructor;
        // its arrival delegates only read the host when a door is entered.
        var poiWorld = new PoiWorld(
            poiSpawner, poiDirectory, poiStore, deliveryBoard, deliveryCareer,
            city, storeCatalog, zone, jobs, onboardingStore, shops, dayCycle, _log);
        // The trip-start breakdown roll (PORTPLAN 39): one seeded instance,
        // never the shared global (AGENTS.md), read by both job flows.
        var breakdowns = new TripBreakdowns(
            new BreakdownStrategy(maintenanceRules, Environment.TickCount),
            garage, onboardingStore, driver, rivals, saver, beat, _log);
        // The taxi job from proposal to receipt; the shell attaches itself as
        // its host right after construction.
        var taxi = new TaxiJobFlow(
            taxiProposals, addressMap, carBrands, cars, requests, requestTable,
            police, taxiRules, fares, garage, driver, friendships, fleet, game, wall,
            beat, saver, rivals, breakdowns, hud, _log);
        // The delivery run from DELIVER to receipt, with its own board page.
        var delivery = new DeliveryFlow(
            deliveryBoard, deliveryCareer, city, friendships, meter, breakdowns,
            beat, _log);
        // The Cento Miglia entry (PORTPLAN 49): the drive to the start, the
        // registration check and the race to the finish, in its own collaborator
        // so neither job flow grows a third phase machine. Its settle prices the
        // receipt apart from the taxi/delivery effect order.
        var cento = new CentoRaceFlow(driver, garage, beat, _log);
        var centoSettlement = new CentoRaceSettlement(
            driver, garage, trunk, centoRaceRules, new Random(taxiSeed + 7), beat, _log);
        // The car haul (PORTPLAN 20): settle-only today, so the tap prices the
        // placeholder leg, banks it and hands back the flow's own receipt.
        var haul = new CarHaulFlow(carBrands, cars, driver, dayCycle, saver, beat, _log);
        // The weekly market valuation (PORTPLAN 49): its own flow owns the
        // Monday week, the venue leg and the live/offline comparison, so the
        // coordinator only forwards the calendar tap, the poll and the one
        // ENTER the HUD raises at the venue. Its draws get their own seeded
        // stream, offset from the other launch draws.
        var valuationFlow = new ValuationFlow(
            zone, game, wall, valuation, valuationBoard, garage, cars, driver,
            profileStore, hud, saver, rivals, beat, new Random(taxiSeed + 8), _log,
            ignoreEventGates: IgnoreEventGatesForTesting);
        // The venue is a temporary door like the quest's markers: it joins the
        // typed list once its flow exists, so ENTER opens the showcase instead
        // of falling through to a business interior.
        poiHandlers = [.. poiHandlers, new ValuationPoiHandler(valuationFlow)];
        var poiArrivals = new PoiArrivalRouter(poiWorld.Handlers(poiHandlers));
        // Saturday's Touristenfahrten run: its own content table, door service,
        // settle and flow. The run is lap catering around the circuit, so its
        // shop/landmark doors may sit outside the operating zone and draw over
        // the whole placeholder map; the directory banks them like any other
        // discovered door.
        TouristenMenu touristenMenu = new JsonTouristenMenu(_log).Load();
        var touristenDoors = new TouristenDoors(
            poiDirectory, poiStore, poiSpawner, addressMap, touristenMenu,
            new Random(taxiSeed + 11), _log);
        var touristenSettlement = new TouristenfahrtenSettlement(
            driver, deliveryFares, fleet, rarity, rivals, beat, _log);
        var touristen = new TouristenfahrtenFlow(
            touristenMenu, touristenDoors, hud, touristenSettlement, meter,
            taxiRules, new Random(taxiSeed + 12), _log);
        // The page graph: the control page, the menu and every tile behind
        // them, with their own caches. It takes the flows (their pages are
        // theirs) and the session's data; the coordinator attaches as its host
        // inside the VM's constructor, which is the one late edge for the
        // entries that are not pages (a timed-out shift, the settings page).
        // The ToDo list (PORTPLAN 46) is a collaborator of its own: it owns
        // the ledger and its file, and the jobs page reads only its lines.
        var todoBoard = new TodoBoard(
            todos, todoStore, poiDirectory, onboardingStore, driver, jobs, zone,
            garage, pins, visits, deliveryCareer, _log);
        var navigation = new Navigation(
            shell, beat, game, wall, shift, todoBoard, roadTests, saver,
            driver, garage, rivals, zoneStandings, trunk, hunger, storeCatalog,
            poiDirectory, hud, deliveryBoard, deliveryCareer, city,
            taxi, delivery, haul, poiWorld, dayCycle, zone, centoTiers,
            addressMap, () => profileStore.Load()?.StreetId, cento,
            valuationFlow, willyFlow, willyLedger, roadTestFlow, touristen,
            // The Cento Miglia registration draw: a page-local number, seeded
            // off the same launch seed the other draws use.
            () => carBrands.Brand, new Random(taxiSeed + 6), _log,
            ignoreEventGates: IgnoreEventGatesForTesting);
        // The panel's reveal policy: what a call opens, what revealing a POI
        // or a job page does, and hiding. The story flow, the voice router and
        // the coordinator share this one instance, so the root can build the
        // two host reads the coordinator used to assemble.
        var panel = new PanelPresenter(
            shell, navigation, taxi, delivery, touristen, wall, _log);
        // The introduction walk and its errand: it reports through its own
        // host (page swaps, panel reveals, the shared driver save), so it is
        // built here like every other collaborator. Its car brand is read live
        // off the watcher, the week off the calendar.
        // The mic is built by the root and handed to both sides that need it:
        // the coordinator owns its session lifecycle, the story flow only gets
        // the two verbs the consent page's answer needs.
        IVoiceListener voice = CreateVoice(_log);
        var story = new OnboardingFlow(
            onboardingStore, content, profileStore, playerNames, addressMap, playerName,
            poiGenerator, poiDirectory, poiStore, poiArrivals, trunk,
            deliveryCareer, deliveryBoard.Catalog.StartCredP,
            preferencesStore, preferences, culture, beat, hud, garage,
            () => dayCycle.DisplayWeek, saver.SaveGarage, () => carBrands.Brand,
            _log, new StoryHost(panel, navigation, saver, rivals, todoBoard),
            voice.StartListening, voice.StopListening);
        // Voice dispatch: the wake word and the button taps, on the same panel
        // and content tables the screens use.
        var voiceRouter = new VoiceRouter(panel, carBrands, content, wall, _log, hud);
        // Settings' QUIT closes the main panel, the app's one exit path: the
        // window's close handlers bank the session and App.xaml shuts down on
        // that close, so it never needs a hard Environment.Exit. The window is
        // built below, hence the deferred read.
        Action quit = () => MainWindow?.Close();
        var vm = new MainViewModel(
            new ShmReader(_log),
            wall,
            world,
            shift,
            new JsonSettingsStore(),
            _log,
            voice,
            carBrands,
            addressMap,
            taxiRules,
            beat,
            driver,
            rivals,
            garage,
            catchUp, zoneGrowth, saver, fleet, settlement, shell, panel, story, voiceRouter,
            taxi, delivery, poiWorld, cento, centoSettlement, valuationFlow, willyFlow,
            roadTestFlow, touristen,
            navigation, readouts, dayCycle,
            todoBoard, pins,
            hud,
            saveResets, restarter, quit);

        MainWindow = new MainWindow(vm, _log, story.Done);
        // The 200 px companion: own placement file, width follows the main
        // panel, and it outlives the main window's hide/show because it is a
        // separate top-level window. Its VM keeps it hidden through the
        // introduction and reveals it when the walk's closing car page is
        // confirmed.
        var hudWindow = new HudWindow(
            hud, HudSettings(), MainWindow.Left, MainWindow.Top, MainWindow.Width);
        // A page can hand its task text to the companion while the panel is
        // still open (an address was just pinned): show it at once instead of
        // waiting for the panel to hide.
        hud.TaskTextRequested += (_, _) => hudWindow.ShowTaskTextOverPanel();
        MainWindow.SizeChanged += (_, _) => hudWindow.MatchWidth(MainWindow.ActualWidth);
        // The companion sits in the same corner as the panel; hide it while
        // the panel is on screen so it cannot cover page rows or eat clicks.
        // Closing the panel (app exit) must not count as "back to driving".
        bool closing = false;
        MainWindow.Closing += (_, _) => closing = true;
        MainWindow.IsVisibleChanged += (_, _) =>
        {
            if (!closing)
            {
                hudWindow.SetPanelOpen(MainWindow.IsVisible);
            }
        };
        // WHY: the app opens on the page the saved progress points at (a
        // resumed walk page, or the menu once the introduction is done) even
        // when the microphone is ready. A wake-word-only start used to leave
        // the panel hidden (and the first-run pages unseen) until the player
        // happened to say the phrase. The VM still owns visibility, so
        // idle-hide, the hotkey and the wake word keep working; the companion
        // HUD stays hidden through the walk until the closing car page hands
        // the errand over (a save past the walk reveals it at startup).
        // A save that resumes mid-tour stays hidden: the tour is still the
        // introduction and is driven on the HUD, so opening the menu here
        // would put the panel up before the intro is finished. The gate is the
        // story flow's live value, like the window's placement gate.
        if (!story.IntroTourInProgress)
        {
            vm.Show();
        }
        if (vm.IsShown)
        {
            MainWindow.Show();
        }
    }

    // A saved door whose id is exactly a spawnable definition's id came from
    // the retired at-start boot seed; generated doors suffix the id with the
    // family index ("carwash_0") and are never mistaken for it.
    private static bool IsRetiredAtStartDoor(
        PointOfInterest poi, IReadOnlyList<PoiDefinition> table)
    {
        foreach (PoiDefinition definition in table)
        {
            if (definition.Spawn is not null
                && string.Equals(poi.Id, definition.Id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The HUD banks its own placement beside the main window.json so resizing
    // or moving one never rewrites the other's file. Both live in the profile
    // folder, so a demo build keeps its own copy.
    private static JsonSettingsStore HudSettings() => new(DataPaths.UserSave("hud.json"));

    // Mic model lookup: %AppData% first (drop the Vosk model folder here),
    // then EVO_VOSK_MODEL, then Data next to the exe. No model = visible
    // window, voice off; the app must never fail to start over voice.
    // EVO_MIC optionally pins the input (substring, e.g. "Intel" or the
    // headphone array); unset follows the Windows default capture device,
    // which is what the Sound settings "mic turned on" toggle routes.
    private static IVoiceListener CreateVoice(FileLogger log)
    {
        string[] candidates =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AceDrive", "vosk-model"),
            Environment.GetEnvironmentVariable("EVO_VOSK_MODEL") ?? "",
            Path.Combine(AppContext.BaseDirectory, "Data", "vosk-model"),
        ];
        foreach (string dir in candidates)
        {
            if (dir.Length > 0 && Directory.Exists(dir))
            {
                return new VoskListener(dir, log, micPreference: Environment.GetEnvironmentVariable("EVO_MIC"));
            }
        }

        log.LogWarning("no vosk-model found; voice off, window stays visible");
        return new NullVoiceListener();
    }

    // Last resort: log the crash for diagnostics, tell the user where the
    // log lives, shut down instead of dying silent (AGENTS.md robustness).
    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            _log?.LogError($"unhandled UI exception: {e.Exception}");
            MessageBox.Show(
                Strings.Format("FatalCrashMessage", LogPath),
                Strings.Get("FatalCrashTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            // WHY: last-resort handler: the log sink can fail like any file
            // write, and a MessageBox without a usable desktop answers with
            // COM/invalid-operation errors. Nothing inside this catch may
            // escape — the process is already dying and must still reach
            // the Shutdown below (AGENTS.md: never throw out of shutdown).
        }
        e.Handled = true;
        Shutdown(1);
    }

    private static string LogPath => DataPaths.UserLogs;

    protected override void OnExit(ExitEventArgs e)
    {
        _log?.LogInfo("AceDrive exiting.");
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // The valuation watcher stops before the connection it reads through:
        // its own cancel token is linked to the session's, and a disposed
        // client must not be asked for one more list.
        _valuationBoard?.Dispose();
        _valuationBoard = null;
        _firebase?.Dispose();
        _firebase = null;
        _log?.Dispose();
        _log = null;
        GC.SuppressFinalize(this);
    }
}
