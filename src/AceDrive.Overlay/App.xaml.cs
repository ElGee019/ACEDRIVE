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
        var content = new JsonOnboardingContent(_log).Load();
        var deliveryCatalog = new JsonDeliveryCatalog(_log).Load();

        var vm = new MainViewModel(
            new ShmReader(_log),
            new SystemClock(),
            new TaxiShift(),
            new JsonSettingsStore(),
            _log,
            fares,
            new JsonOnboardingStore(),
            content,
            CreateVoice(_log),
            new CarLogWatcher(_log),
            todos,
            deliveryCatalog);

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
        catch { /* already failing; just exit */ }
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
