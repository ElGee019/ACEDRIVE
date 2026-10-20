using System.IO;
using System.Windows;
using System.Windows.Threading;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Overlay;

/// <summary>
/// Sole composition root (DIP): the only place that news up services.
/// Views and ViewModels never construct their own dependencies.
/// Owns the process-wide logger, hence IDisposable (CA1001).
/// </summary>
public partial class App : Application, IDisposable
{
    private FileLogger? _log;
    private bool _disposed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;

        _log = new FileLogger();
        _log.LogInfo("AceDrive starting...");
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
        // WHY TickCount: like the road-test ledger, the proposal draw wants
        // its own seeded instance (never the shared global) and only the taxi
        // page uses it; tests pin the seed.
        var taxiProposals = new TaxiJobProposer(
            new JsonNamePoolStore(_log).Load(), Environment.TickCount);
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
        var addressMap = new JsonAddressMapStore(_log).Load();
        // The story POIs (PORTPLAN 5/24/27): names from Data/pois.json, each
        // launch drawing a fresh random house for the birthday errand. WHY
        // TickCount: its own seeded instance, never the shared global; tests
        // pin the seed.
        var poiTable = new JsonPoiStore(_log).Load();
        var poiGenerator = new PoiGenerator(addressMap, poiTable, Environment.TickCount);
        // Shop content (PORTPLAN items 24/26): the item database + per-store
        // layout. A missing/corrupt file degrades to the built-in catalog.
        var storeCatalog = new JsonStoreItemStore(_log).Load();
        var stores = new StoreNetwork(storeCatalog, poiTable);
        var playerName = new SteamPersonaNameSource(_log).TryReadName();
        var deliveryCatalog = new JsonDeliveryCatalog(_log).Load();
        // The delivery distance-charge table rides beside the taxi fares:
        // one file each, so balance edits never collide.
        var deliveryFares = new JsonFareStore(_log).LoadDelivery();
        // The career state is the delivery side's wallet + hub list; Max's
        // intro gift seeds it (PORTPLAN 5/51: the only CredP that exists
        // until the persistent save lands).
        var deliveryCareer = new DeliveryCareer(new JsonHubCatalog(_log).Load(), deliveryCatalog.StartCredP);

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
        // The trunk rides the driver state (Unity PlayerData.trunkItems), so
        // bought goods and the spent cash survive a restart together.
        var trunk = new Trunk(savedDriver?.Trunk);
        // The login streak (PORTPLAN 47): recomputed from the saved dates at
        // every boot, so the counter is always fresh against today.
        var streak = new DailyStreak(wall, savedDriver);
        // The operating zone (PORTPLAN 58): the street-growth counters resume
        // from the last session; the threshold curve re-draws its seeded
        // lottery per launch, Unity GetNextExpansionThreshold parity.
        var zone = new OperatingZone(savedDriver, Environment.TickCount);
        // XP world standings (PORTPLAN 60): the offline null-object until
        // the online backend lands. The saved rank snapshot is restored at
        // boot (Unity's LoadFromPlayerData), so receipts can show the last
        // known standing while the board itself is still out of reach.
        var rivals = new OfflineRivals();
        rivals.RestoreSnapshot(savedDriver);
        // Familiarity (PORTPLAN 45): one persisted level per resident, so a
        // restart reopens the same friendships instead of resetting them.
        var friendships = new JsonFriendshipStore(_log);
        // Brand mileage (PORTPLAN 35): lifetime km per driven brand behind
        // the receipt's sponsorship rate, resumed across sessions.
        var brands = new JsonBrandMileageStore(_log);
        // The typed POI router (PORTPLAN item 24): Max's grocery is the first
        // interior. A future business joins by adding its handler here.
        var poiArrivals = new PoiArrivalRouter(
        [
            new StorePoiHandler(stores, trunk, driver, garage, friendships, beat),
        ]);
        var hud = new HudViewModel(addressMap, preferences, beat);
        var vm = new MainViewModel(
            new ShmReader(_log),
            wall,
            new GameClock(wall, saved?.GameNow ?? new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc)),
            new TaxiShift(),
            new JsonSettingsStore(),
            _log,
            fares,
            taxiProposals,
            new JsonOnboardingStore(),
            content,
            profileStore,
            addressMap,
            playerName,
            CreateVoice(_log),
            new CarLogWatcher(_log),
            todos,
            deliveryCatalog,
            deliveryCareer,
            deliveryFares,
            city,
            cityStore,
            roadTests,
            roadTestStore,
            clockStore,
            saved?.Week ?? CalendarStamp.Default.Week,
            beat,
            driver,
            driverStore,
            streak,
            zone,
            friendships,
            cars,
            brands,
            rivals,
            hunger,
            hungerStore,
            garage,
            garageStore,
            insuranceRules,
            // The trip-start breakdown roll (PORTPLAN 39): a seeded instance,
            // never the shared global (AGENTS.md).
            new BreakdownStrategy(maintenanceRules, Environment.TickCount),
            poiGenerator,
            preferencesStore, preferences, culture,
            hud, trunk, poiArrivals);

        MainWindow = new MainWindow(vm, _log);
        // The 200 px companion: own placement file, width follows the main
        // panel, and it outlives the main window's hide/show because it is a
        // separate top-level window. Its VM keeps it hidden through the
        // introduction and reveals it when the birthday dialogue ends.
        var hudWindow = new HudWindow(
            hud, HudSettings(), MainWindow.Left, MainWindow.Top, MainWindow.Width);
        MainWindow.SizeChanged += (_, _) => hudWindow.MatchWidth(MainWindow.ActualWidth);
        // The VM owns visibility: with a mic we start hidden until the wake
        // word. Showing first and hiding on Loaded left a visible flash.
        if (vm.IsShown)
        {
            MainWindow.Show();
        }
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
        _log?.Dispose();
        _log = null;
        GC.SuppressFinalize(this);
    }
}
