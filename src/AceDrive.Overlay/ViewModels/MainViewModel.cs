using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// All overlay behaviour. Dependencies are constructor-injected (DIP);
/// time, telemetry, logging and fares are abstracted so every flow is
/// unit-testable with fakes. Knows NOTHING about HWNDs or files.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IScreenController, IVoiceInfoNavigator, IDisposable
{
    private readonly ITelemetrySource _telemetry;
    private readonly IClock _clock;
    private readonly TaxiShift _shift;
    private readonly ISettingsStore _store;
    private readonly ILog _log;
    private readonly FareTable _fares;
    private readonly IOnboardingStore _onboarding;
    private readonly OnboardingContent _content;
    private readonly TodoTable _todos;
    private readonly IVoiceListener _voice;
    private readonly ICarBrandSource _carBrands;
    private readonly DeliveryCatalog _deliveryCatalog;
    private OnboardingState _progress;
    private MainControlScreenViewModel? _control;
    private DeliveryJobScreenViewModel? _deliveryScreen;

    // Companion parity: hidden until "hey {brand}", idle hides after 9 s,
    // one utterance fires one button (muted until the final fragment).
    private const double IdleHideSec = 9.0;
    private const double WakeCooldownSec = 3.0;
    private const string DefaultBrand = "mazda";
    private DateTime _lastActivity;
    private DateTime _lastWake = DateTime.MinValue;
    private string? _mutedLabel;
    private Action? _mutedAction;
    private bool _resizing;

    // Wake brand = the car currently driven in ACE (watcher-published),
    // never the saved favorite.
    private string _carBrand = DefaultBrand;
    private readonly DispatcherTimer _timer;
    private DateTime _lastTick;
    private bool _disposed;

    // Placeholder destinations until the Eifel address table lands (PORTPLAN Step 1).
    private static readonly (string From, string To)[] FakeJobs =
    {
        ("Adenau Post", "Nürburg Castle"),
        ("Breidscheid Bakery", "Aremberg Viewpoint"),
        ("Herschbroich Garage", "Quiddelbacher Höhe"),
        ("Nürburg Ring Werk", "Adenau Marketplace"),
    };
    private int _jobIndex;
    private TaxiJobState _job = new();
    private int _lastShiftSecs = -1;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(
        ITelemetrySource telemetry, IClock clock,
        TaxiShift shift, ISettingsStore store,
        ILog log, FareTable fares,
        IOnboardingStore onboarding, OnboardingContent content,
        IVoiceListener voice, ICarBrandSource carBrands,
        TodoTable todos, DeliveryCatalog deliveryCatalog)
    {
        _telemetry = telemetry;
        _clock = clock;
        _shift = shift;
        _store = store;
        _log = log;
        _fares = fares;
        _onboarding = onboarding;
        _content = content;
        _todos = todos;
        _voice = voice;
        _carBrands = carBrands;
        _deliveryCatalog = deliveryCatalog;
        _carBrands.BrandChanged += (_, e) => OnCarBrand(e.Brand);
        _progress = _onboarding.Load();
        _lastActivity = _clock.UtcNow;
        _voice.Heard += (_, e) => OnVoiceHeard(e.Text, e.IsFinal);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Poll();
        _lastTick = _clock.UtcNow;

        NewJobCommand = new RelayCommand(NewJob);
        PickupCommand = new RelayCommand(Pickup, () => _job.Phase == JobPhase.Proposed);
        DropoffCommand = new RelayCommand(Dropoff, () => _job.Phase == JobPhase.EnRoute);
        ToggleShowCommand = new RelayCommand(() =>
        {
            if (IsShown) Hide(); else Show();
        });

        RefreshJob();
        StatusText = Strings.Get("WaitingForEvo");
        SpeedText = Strings.Format("SpeedKmh", 0);
        GearText = Strings.Get("GearUnknown");
        DetailText = string.Empty;

        // Companion parity: the brand page is the onboarding home — what the
        // window shows at boot and on every wake until onboarding completes.
        // Past that, home is the main control page (PORTPLAN item 54); its
        // date header replaces the onboarding titles. Empty content degrades
        // to a blank screen instead of trapping the user on a broken page.
        SetScreen(HomeScreen());

        // With a mic we start hidden until the wake word; without one the
        // window stays up so the app remains usable (and testable).
        IsShown = !voice.IsAvailable;
    }

    // ---- bindable state -------------------------------------------------

    private bool _isShown;
    public bool IsShown
    {
        get => _isShown;
        private set => SetField(ref _isShown, value);
    }

    private string _statusText = "";
    public string StatusText { get => _statusText; private set => SetField(ref _statusText, value); }

    private bool _isLive;
    public bool IsLive { get => _isLive; private set => SetField(ref _isLive, value); }

    private string _speedText = "";
    public string SpeedText { get => _speedText; private set => SetField(ref _speedText, value); }

    private string _gearText = "";
    public string GearText { get => _gearText; private set => SetField(ref _gearText, value); }

    private string _detailText = "";
    public string DetailText { get => _detailText; private set => SetField(ref _detailText, value); }

    private string _jobLine = "";
    public string JobLine { get => _jobLine; private set => SetField(ref _jobLine, value); }

    private string _fareText = "";
    public string FareText { get => _fareText; private set => SetField(ref _fareText, value); }

    private string _shiftText = "";
    public string ShiftText { get => _shiftText; private set => SetField(ref _shiftText, value); }

    public ICommand NewJobCommand { get; }
    public ICommand PickupCommand { get; }
    public ICommand DropoffCommand { get; }
    public ICommand ToggleShowCommand { get; }

    // ---- onboarding navigation (IScreenController) ---------------------------

    private object? _currentScreen;

    /// <summary>Active screen: an onboarding page, a jobs page or null.</summary>
    public object? CurrentScreen
    {
        get => _currentScreen;
        private set
        {
            if (SetField(ref _currentScreen, value))
            {
                OnPropertyChanged(nameof(IsOnboarding));
                OnPropertyChanged(nameof(JobsVisible));
            }
        }
    }

    public bool IsOnboarding => CurrentScreen is not null && CurrentScreen is not IJobsScreen;
    public bool JobsVisible => CurrentScreen is null;

    public void ChooseBrand(string brand)
    {
        if (!_content.Brands.Contains(brand)) return;
        _progress = _progress with { FavoriteBrand = brand };
        _onboarding.Save(_progress);
        _log.LogInfo($"favorite brand: {brand}");
        SetScreen(new GrandmaCarScreenViewModel(this, _content.Cars));
    }

    public void ChooseCar(string carId)
    {
        if (!_content.Cars.Any(c => c.Id == carId)) return;
        _progress = _progress with { GrandmaCarId = carId };
        _onboarding.Save(_progress);
        _log.LogInfo($"grandma car: {carId}");
        SetScreen(new VoiceInfoScreenViewModel(this));
    }

    public void ShowDialogue()
    {
        _log.LogInfo("voice info acknowledged: showing birthday dialogue");
        SetScreen(new DialogueScreenViewModel(this, _content.Dialogue));
    }

    public void FinishDialogue()
    {
        _progress = _progress with { BirthdayDone = true };
        _onboarding.Save(_progress);
        _log.LogInfo("birthday dialogue done: main control page unlocked");
        SetScreen(CreateControlScreen());
    }

    // Onboarding home (brand page) or the control page once complete.
    // No new IScreenController verbs: the control page takes its job
    // commands by constructor (composition over interface growth).
    private object? HomeScreen()
    {
        if (_progress.IsComplete)
        {
            return CreateControlScreen();
        }
        if (_content.Brands.Count == 0)
        {
            return null;
        }
        return new BrandScreenViewModel(this, _content.Brands);
    }

    // Built once and reused: the date header stays live across wakes.
    // Taxi reuses the existing fake-job flow; delivery opens the career
    // page (port of Unity DeliveryJobScreen); the calendar day buttons have
    // no destinations until the freeroam map lands, so they stay disabled
    // (and voiceless).
    private MainControlScreenViewModel CreateControlScreen() =>
        _control ??= new MainControlScreenViewModel(
            _clock, _todos, CalendarStamp.Default, _shift,
            NewJobCommand, new RelayCommand(OpenDelivery),
            new RelayCommand(OnDayLocked, () => false));

    private void OnDayLocked() =>
        _log.LogInfo("calendar day tapped while locked: waiting on the freeroam map");

    // Built once and reused: the pickup cart survives leaving the page, like
    // Unity's player.pickupCart. Back lands on the control page. The hub and
    // run buttons are navigation seams only: hub purchase needs the wholesale
    // content (PORTPLAN 18) and the pickup/drop-off wizard the freeroam map,
    // so both just log until those pages land.
    private void OpenDelivery()
    {
        _deliveryScreen ??= new DeliveryJobScreenViewModel(
            _deliveryCatalog, _log,
            new RelayCommand(() => SetScreen(CreateControlScreen())),
            new RelayCommand(OnDeliveryRun),
            new RelayCommand(OnDeliveryHub));
        SetScreen(_deliveryScreen);
    }

    private void OnDeliveryRun() =>
        _log.LogInfo("deliver tapped: pickup wizard lands with the freeroam map");

    private void OnDeliveryHub() =>
        _log.LogInfo("open new career tapped: hub purchase lands with the wholesale content");

    private void SetScreen(object? screen) => CurrentScreen = screen;

    // ---- lifecycle (called by the view) ----------------------------------

    public void Start()
    {
        _lastTick = _clock.UtcNow;
        _carBrand = _carBrands.Prime(DefaultBrand);
        _log.LogInfo($"listening for 'hey {_carBrand}'");
        _carBrands.StartWatching();
        _voice.StartListening();
        _timer.Start();
    }

    public WindowBounds? LoadPlacement() => _store.Load();
    public void SavePlacement(WindowBounds bounds) => _store.Save(bounds);

    // ---- core loop (public so tests drive it directly) --------------------

    public void Poll()
    {
        var now = _clock.UtcNow;
        double dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        // Idle auto-hide, companion parity. Must run before any telemetry
        // early-return: hiding is independent of whether EVO is live.
        // Suspended while the window is being resized: the sizing drag
        // itself is the user's activity, even when held still.
        // Suspended while a dialogue is still open (IDialogueScreen): a
        // slow reader is never hidden mid-conversation.
        // Uses the clock, never tick counts.
        bool dialogueOpen = CurrentScreen is IDialogueScreen dialogue && !dialogue.IsComplete;
        if (IsShown && !_resizing && !dialogueOpen && (now - _lastActivity).TotalSeconds >= IdleHideSec)
        {
            Hide();
        }

        // The control page date stays live whether or not EVO is attached.
        // Refresh self-guards on the minute: no per-tick allocations.
        // A timed-out shift ends here (Unity 30-min idle rule) so the taxi
        // button flips back to START even when the player never taps it.
        if (_shift.IsTimedOut(now) && _shift.TryEndTimedOut(now, out TimeSpan idle))
        {
            _log.LogInfo($"shift ended (timeout after {idle.TotalMinutes:0}m idle): " +
                $"{_shift.JobsDone} jobs / {_shift.Earned:0.00}");
            _lastShiftSecs = -1;
            RefreshJob();
        }
        // Voice-flash expiry runs on the clock, not the minute guard: a 1 s
        // yellow glow must clear inside the same minute it started.
        _control?.UpdateFlash();
        _control?.Refresh();

        // Shift countdown: second-guarded so an open-but-idle shift costs one
        // string per second, never per 10 Hz tick. While EnRoute the meter
        // below already repaints every tick, so this path skips it.
        if (_shift.IsStarted && _job.Phase != JobPhase.EnRoute)
        {
            int secs = (int)_shift.Remaining(now).TotalSeconds;
            if (secs != _lastShiftSecs)
            {
                _lastShiftSecs = secs;
                RefreshJob();
            }
        }
        else if (!_shift.IsStarted)
        {
            _lastShiftSecs = -1;
        }

        var s = _telemetry.TryRead();
        if (s is null)
        {
            IsLive = false;
            StatusText = Strings.Get(_telemetry.GameRunning ? "EvoNoShm" : "WaitingForEvo");
            SpeedText = Strings.Format("SpeedKmh", 0);
            return;
        }

        IsLive = true;
        StatusText = Strings.Format("EvoLive", s.PacketId);
        SpeedText = Strings.Format("SpeedKmh", s.SpeedKmh);
        GearText = TelemetryConventions.GearDisplay(s.Gear);
        DetailText = Strings.Format("EvoDetail", s.Rpm, s.Gas, s.Brake);

        // Meter runs while the passenger is aboard.
        if (_job.Phase == JobPhase.EnRoute && dt is > 0 and < 1)
        {
            _job.DistanceKm += Math.Max(0, s.SpeedKmh) / 3600.0 * dt;
            _job.Fare = TaxiFare.ComputeFare(_job.DistanceKm, _fares);
            RefreshJob();
        }
    }

    // ---- visibility + voice --------------------------------------------------

    /// <summary>Called by the view on any mouse/key activity.</summary>
    public void RecordActivity() => _lastActivity = _clock.UtcNow;

    /// <summary>
    /// Called by the view when a window resize drag starts/stops.
    /// While resizing, idle auto-hide is suspended; both edges count as
    /// activity so the 9 s window restarts after the drag.
    /// </summary>
    public void SetResizing(bool resizing)
    {
        _resizing = resizing;
        RecordActivity();
    }

    public void Show()
    {
        RecordActivity();
        if (!IsShown)
        {
            IsShown = true;
            _log.LogInfo("overlay shown");
        }
    }

    private void Hide()
    {
        if (!IsShown) return;
        IsShown = false;
        _mutedLabel = null;
        _mutedAction = null;
        _log.LogInfo("overlay hidden");
    }

    // Watcher thread lands here; marshal to the UI thread like voice.
    private void OnCarBrand(string brand)
    {
        try
        {
            _timer.Dispatcher.Invoke(() =>
            {
                _carBrand = brand;
                _log.LogInfo($"now following: {brand} (say 'hey {brand}')");
            });
        }
        catch { /* shutdown race; never break the watcher thread */ }
    }

    // Mic thread lands here; marshal to the UI thread for all state changes.
    private void OnVoiceHeard(string text, bool isFinal)
    {
        try
        {
            _timer.Dispatcher.Invoke(() => HandleVoice(text, isFinal));
        }
        catch { /* shutdown race; never break the mic thread */ }
    }

    internal void HandleVoice(string text, bool isFinal)
    {
        // Diagnostic: one line per finished utterance. Non-English names
        // ("Cento Miglia") arrive as whatever the English model believes it
        // heard; reading the real transcripts from the log lets us extend
        // the keyword tables from data instead of guesses.
        if (isFinal) _log.LogInfo($"voice heard: {text}");

        if (!IsShown)
        {
            string brand = _carBrand;
            if (VoiceMatch.IsWake(text, brand, AliasesFor(brand)))
            {
                DateTime now = _clock.UtcNow;
                if ((now - _lastWake).TotalSeconds < WakeCooldownSec) return;
                _lastWake = now;
                _log.LogInfo($"wake heard: {text}");
                Show();
                SetScreen(HomeScreen());
            }

            return;
        }

        if (CurrentScreen is not IVoiceScreen screen) return;
        string? label = VoiceMatch.MatchLabel(text, screen.VoiceActions.Keys);
        if (label is null) return;
        // Voice control counts as user input: same idle-reset as a click.
        // Repeats of the same tap still count as presence (restarting the
        // 9 s window) even though the repeat itself stays muted below.
        RecordActivity();
        // Mute repeats of the same tap until the utterance's final fragment.
        // One utterance can reach a button under two different keys (a
        // partial hearing "where is" alias, the final "Where is Willy"
        // label), so the shared action delegate counts as the same tap too.
        Action action = screen.VoiceActions[label];
        if (label == _mutedLabel || action == _mutedAction)
        {
            if (isFinal) { _mutedLabel = null; _mutedAction = null; }
            return;
        }

        _mutedLabel = label;
        _mutedAction = action;
        _log.LogInfo($"voice button: {text}");
        action.Invoke();
    }

    private string[] AliasesFor(string brand)
    {
        if (_content.Aliases.TryGetValue(brand, out string[]? hit)) return hit;
        return _content.Aliases.TryGetValue(VoiceMatch.Normalize(brand), out string[]? norm) ? norm : [];
    }

    // ---- job commands ------------------------------------------------------

    private void NewJob()
    {
        DateTime now = _clock.UtcNow;
        if (_shift.IsTimedOut(now) && _shift.TryEndTimedOut(now, out TimeSpan idle))
        {
            _log.LogInfo($"shift ended (timeout after {idle.TotalMinutes:0}m idle): " +
                $"{_shift.JobsDone} jobs / {_shift.Earned:0.00}");
        }
        if (!_shift.IsStarted)
        {
            _shift.Start(now);
            _lastShiftSecs = -1;
            _log.LogInfo("taxi shift started");
        }
        var (from, to) = FakeJobs[_jobIndex % FakeJobs.Length];
        _jobIndex++;
        _job = new TaxiJobState
        {
            Phase = JobPhase.Proposed,
            PickupName = from,
            DropoffName = to,
            Fare = TaxiFare.ComputeFare(0, _fares),
        };
        _log.LogInfo($"job proposed: {from} -> {to}");
        RefreshJob();
        _control?.Refresh();
    }

    private void Pickup()
    {
        if (_job.Phase != JobPhase.Proposed) return;
        _job.Phase = JobPhase.EnRoute;
        _job.DistanceKm = 0;
        _log.LogInfo($"job pickup: {_job.PickupName}");
        RefreshJob();
    }

    private void Dropoff()
    {
        if (_job.Phase != JobPhase.EnRoute) return;
        _job.Phase = JobPhase.Complete;
        _job.Fare = TaxiFare.ComputeFare(_job.DistanceKm, _fares);
        _job.Tip = TaxiFare.ComputeTip(_job.Fare, cleanDrive: true, _fares);
        _shift.RecordCompleted(_job, _clock.UtcNow);
        _log.LogInfo($"job dropoff: {_job.PickupName} -> {_job.DropoffName} " +
                  $"dist={_job.DistanceKm:0.00}km total={_job.Total:0.00} " +
                  $"shift={_shift.JobsDone} jobs / {_shift.Earned:0.00}");
        RefreshJob();
    }

    private void RefreshJob()
    {
        JobLine = _job.Phase switch
        {
            JobPhase.None => Strings.Get("NoJob"),
            JobPhase.Proposed => Strings.Format("JobProposed", _job.PickupName, _job.DropoffName),
            JobPhase.EnRoute => Strings.Format("JobEnRoute", _job.PickupName, _job.DropoffName, _job.DistanceKm),
            JobPhase.Complete => Strings.Format("JobComplete", _job.PickupName, _job.DropoffName, _job.Tip),
            _ => string.Empty,
        };
        FareText = Strings.Format("FareTotal", _job.Total);
        ShiftText = _shift.IsStarted
            ? Strings.Format("ShiftSummaryActive",
                _shift.JobsDone, _shift.Earned, FormatRemaining(_shift.Remaining(_clock.UtcNow)))
            : Strings.Format("ShiftSummary", _shift.JobsDone, _shift.Earned);
        ((RelayCommand)PickupCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DropoffCommand).RaiseCanExecuteChanged();
    }

    private static string FormatRemaining(TimeSpan remaining) =>
        remaining.ToString(@"m\:ss", CultureInfo.InvariantCulture);

    // ---- plumbing ------------------------------------------------------------

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _voice.Dispose();
        _carBrands.Dispose();
        _telemetry.Dispose();
    }
}
