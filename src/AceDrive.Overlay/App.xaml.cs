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
    private bool _disposed;

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
        var taxiProposals = new TaxiJobProposer(addressMap, residents, taxiSeed + 1);
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
        var poiTable = new JsonPoiStore(_log).Load();
        var poiGenerator = new PoiGenerator(addressMap, poiTable, Environment.TickCount);
        // The business POIs (PORTPLAN 24): no door exists on a new career. The
        // spawner opens the car wash, bank, dealer, mechanic and insurance on
        // free houses as the operating zone and the settled-job count cross
        // Unity's thresholds; the host checks it at boot and on every settle.
        // Separate seeded stream so a spawn never reshuffles a resident name.
        var poiSpawner = new PoiSpawner(addressMap, poiTable, poiNames, taxiSeed + 4);
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
        // The career state is the delivery side's wallet + hub list. A new
        // driver starts with nothing; only Max's first store visit after the
        // introduction hands over the intro CredP (Unity firstSceneComplete +
        // hasSeenMaxJobIntro, PORTPLAN 5/51). The banked visit restores that
        // gift on relaunch until the persistent career save lands.
        var onboardingStore = new JsonOnboardingStore();
        OnboardingState onboardingProgress = onboardingStore.Load();
        bool introGiftClaimed = onboardingProgress.MaxDeliveryIntroSeen;
        var deliveryCareer = new DeliveryCareer(
            new JsonHubCatalog(_log).Load(),
            introGiftClaimed ? deliveryCatalog.StartCredP : 0);

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
        // The lifetime settled-job total (PORTPLAN 24, Unity jobsCompleted):
        // the dealer and mechanic POIs gate on it, so it must survive a
        // restart beside the cash it was earned with.
        var jobs = new JobLedger(savedDriver);
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
        // The placeholder map supplies the interim zone length: a 0 km zone
        // would pin the business starter bonus at 100% (PORTPLAN 47/58).
        var zone = new OperatingZone(
            savedDriver, Environment.TickCount, AddressBook.MapLengthKm(addressMap));
        // XP world standings (PORTPLAN 60): the live Firestore board when a
        // config exists, otherwise the offline null-object. The saved rank
        // snapshot restores either way (Unity's LoadFromPlayerData), so the
        // first receipt already knows the last seen standing.
        IRivalsService rivals;
        ICarRaritySource rarity;
        IPlayerTelemetry telemetry;
        IShiftStandings shiftStandings;
        IPlayerNameRegistry playerNames;
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

            var liveRarity = new FirestoreCarRarity(
                new FirestoreCars(_firebase.Database, wall), _log, _firebase.StopToken);
            liveRarity.Start();

            var liveShiftBoard = new FirestoreShiftStandings(
                onlinePlayers, () => _firebase?.Session.Uid,
                () => profileStore.Load()?.DisplayName,
                _log, _firebase.StopToken);
            liveShiftBoard.RestoreSnapshot(savedDriver);
            liveShiftBoard.Start();

            rivals = liveRivals;
            rarity = liveRarity;
            shiftStandings = liveShiftBoard;
            // WHY UidAsync: the boot poll can end a shift that timed out while
            // the game was closed before anonymous auth has finished, and the
            // shift push has no later retry of its own.
            telemetry = new FirestorePlayerTelemetry(
                onlinePlayers,
                new FirestoreCountries(_firebase.Database, wall),
                _firebase.UidAsync, _log, _firebase.StopToken);
            playerNames = new FirestorePlayerNameRegistry(
                _firebase.Database, _firebase.UidAsync, wall, _log);
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
            playerNames = new OfflinePlayerNameRegistry();
        }
        // Familiarity (PORTPLAN 45): one persisted level per resident, so a
        // restart reopens the same friendships instead of resetting them.
        var friendships = new JsonFriendshipStore(_log);
        // Brand mileage (PORTPLAN 35): lifetime km per driven brand behind
        // the receipt's sponsorship rate, resumed across sessions.
        var brands = new JsonBrandMileageStore(_log);
        // The typed POI handlers (PORTPLAN item 24): Max's grocery is the
        // first interior. The business interiors (bank, dealer, …) are built
        // by the host, which owns their destination changes and banking.
        IPoiArrivalHandler[] poiHandlers =
        [
            new StorePoiHandler(stores, trunk, driver, garage, friendships, beat),
        ];
        var taxiRules = new JsonTaxiRulesStore(_log).Load();
        // Customer request content (PORTPLAN item 11): suffix lines, quiz
        // categories and tier chances. Its own seeded stream, offset from the
        // proposal/resident draws so a request roll never reshuffles them.
        var requestTable = new JsonCustomerRequestStore(_log).Load();
        var requests = new CustomerRequestRoller(cars, requestTable, taxiSeed + 2);
        // Speed-challenge patrols (Phase C): one seeded plan per accepted job,
        // offset from the other draws so it never reshuffles them.
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
            garage, city, hunger, roadTests, shift, shifts, jobs);
        // The panel, its page and the activity stamp: view state with an owner
        // the view binds through the shell, not state the coordinator holds.
        var shell = new OverlayShell();
        var shops = new ShopInteriors(
            driver, garage, game, insuranceRules, cars, saver, beat, _log);
        int startWeek = saved?.Week ?? CalendarStamp.Default.Week;
        // The session's calendar: the day flip, the week it rolls and the idle
        // shift timeout with its ended page. The pages and the dealer's floor
        // read the week from here, so no mirror of it lives anywhere else.
        var dayCycle = new DayCycle(
            game, shift, shifts, shiftStandings, telemetry, profileStore,
            rivals, saver, beat, startWeek, _log);
        // The readout formatter the poll applies: the painted strings and their
        // change caches, so no formatting runs on the hot path.
        var readouts = new TelemetryReadouts();
        // The POI world and its arrival router: the store handler rides in from
        // here, the business interiors are appended by the world. The world
        // attaches to the coordinator as its host inside the VM's constructor;
        // its arrival delegates only read the host when a door is entered.
        var poiWorld = new PoiWorld(
            poiSpawner, poiDirectory, poiStore, deliveryBoard, deliveryCareer,
            city, storeCatalog, zone, jobs, onboardingStore, shops, dayCycle, _log);
        var poiArrivals = new PoiArrivalRouter(poiWorld.Handlers(poiHandlers));
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
            beat, meter, saver, rivals, breakdowns, hud, _log);
        // The delivery run from DELIVER to receipt, with its own board page.
        var delivery = new DeliveryFlow(
            deliveryBoard, deliveryCareer, city, friendships, meter, breakdowns,
            beat, _log);
        // The page graph: the control page, the menu and every tile behind
        // them, with their own caches. It takes the flows (their pages are
        // theirs) and the session's data; the coordinator attaches as its host
        // inside the VM's constructor, which is the one late edge for the
        // entries that are not pages (a timed-out shift, the settings page,
        // the haul settle).
        var navigation = new Navigation(
            shell, beat, game, wall, shift, todos, roadTests, saver,
            driver, garage, rivals, trunk, hunger, storeCatalog,
            poiDirectory, hud, deliveryBoard, deliveryCareer, city,
            taxi, delivery, poiWorld, dayCycle, _log);
        // The panel's reveal policy: what a call opens, what revealing a POI
        // or a job page does, and hiding. The story flow, the voice router and
        // the coordinator share this one instance, so the root can build the
        // two host reads the coordinator used to assemble.
        var panel = new PanelPresenter(shell, navigation, taxi, delivery, wall, _log);
        // The introduction walk and its errand: it reports through its own
        // host (page swaps, panel reveals, the shared driver save), so it is
        // built here like every other collaborator. Its car brand is read live
        // off the watcher, the week off the calendar.
        var story = new OnboardingFlow(
            onboardingStore, content, profileStore, playerNames, addressMap, playerName,
            poiGenerator, poiDirectory, poiStore, poiArrivals, trunk,
            deliveryCareer, deliveryBoard.Catalog.StartCredP,
            preferencesStore, preferences, culture, beat, hud, garage,
            () => dayCycle.DisplayWeek, saver.SaveGarage, () => carBrands.Brand,
            _log, new StoryHost(panel, navigation, saver, rivals));
        // Voice dispatch: the wake word and the button taps, on the same panel
        // and content tables the screens use.
        var voiceRouter = new VoiceRouter(panel, carBrands, content, wall, _log, hud);
        var vm = new MainViewModel(
            new ShmReader(_log),
            wall,
            game,
            shift,
            new JsonSettingsStore(),
            _log,
            CreateVoice(_log),
            carBrands,
            addressMap,
            taxiRules,
            deliveryBoard,
            deliveryCareer,
            city,
            beat,
            driver,
            streak,
            zone,
            cars,
            rivals,
            telemetry,
            hunger,
            garage,
            // The streak's random skill gains (PORTPLAN 06/47): its own seeded
            // stream, offset from the taxi seed like the other draws.
            new Random(taxiSeed + 5),
            saver, fleet, settlement, shell, panel, story, voiceRouter,
            taxi, delivery, poiWorld, navigation, readouts, dayCycle,
            hud,
            saveResets, restarter);

        MainWindow = new MainWindow(vm, _log);
        // The 200 px companion: own placement file, width follows the main
        // panel, and it outlives the main window's hide/show because it is a
        // separate top-level window. Its VM keeps it hidden through the
        // introduction and reveals it when the birthday dialogue ends.
        var hudWindow = new HudWindow(
            hud, HudSettings(), _log, MainWindow.Left, MainWindow.Top, MainWindow.Width);
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
        // HUD stays hidden through the walk until the birthday hand-over
        // reveals it (a save past the birthday reveals it at startup).
        // A save that resumes mid-tour stays hidden: the tour is still the
        // introduction and is driven on the HUD, so opening the menu here
        // would put the panel up before the intro is finished.
        if (!vm.IntroTourInProgress)
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
    // or moving one never rewrites the other's file.
    private static JsonSettingsStore HudSettings() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AceDrive", "hud.json"));

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

    private static string LogPath =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AceDrive", "logs");

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
        _firebase?.Dispose();
        _firebase = null;
        _log?.Dispose();
        _log = null;
        GC.SuppressFinalize(this);
    }
}
