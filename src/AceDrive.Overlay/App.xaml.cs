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
        var deliveryCatalog = new JsonDeliveryCatalog(_log).Load();
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
        var roadTests = new RoadTestLedger(
            new JsonCarStore(_log).Load(), Environment.TickCount, roadTestStore.Load());
        // Resume the game moment: a missing or corrupt clock file means a
        // fresh Saturday morning on week 1 (the store never throws).
        var clockStore = new JsonGameClockStore(_log);
        GameClockState? saved = clockStore.Load();
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
            CreateVoice(_log),
            new CarLogWatcher(_log),
            todos,
            deliveryCatalog,
            deliveryCareer,
            city,
            cityStore,
            roadTests,
            roadTestStore,
            clockStore,
            saved?.Week ?? CalendarStamp.Default.Week,
            beat);

        MainWindow = new MainWindow(vm, _log);
        // The VM owns visibility: with a mic we start hidden until the wake
        // word. Showing first and hiding on Loaded left a visible flash.
        if (vm.IsShown)
        {
            MainWindow.Show();
        }
    }

    // Mic model lookup: %AppData% first (drop the Vosk model folder here),
    // then EVO_VOSK_MODEL, then Data next to the exe. No model = visible
    // window, voice off; the app must never fail to start over voice.
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
                return new VoskListener(dir, log);
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
