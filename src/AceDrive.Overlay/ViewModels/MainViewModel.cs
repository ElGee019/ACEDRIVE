using System.ComponentModel;
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
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ITelemetrySource _telemetry;
    private readonly IClock _clock;
    private readonly TaxiShift _shift;
    private readonly ISettingsStore _store;
    private readonly ILog _log;
    private readonly FareTable _fares;
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

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel(
        ITelemetrySource telemetry, IClock clock,
        TaxiShift shift, ISettingsStore store,
        ILog log, FareTable fares)
    {
        _telemetry = telemetry;
        _clock = clock;
        _shift = shift;
        _store = store;
        _log = log;
        _fares = fares;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Poll();
        _lastTick = _clock.UtcNow;

        NewJobCommand = new RelayCommand(NewJob);
        PickupCommand = new RelayCommand(Pickup, () => _job.Phase == JobPhase.Proposed);
        DropoffCommand = new RelayCommand(Dropoff, () => _job.Phase == JobPhase.EnRoute);
        ToggleLockCommand = new RelayCommand(() => IsLocked = !IsLocked);

        RefreshJob();
        StatusText = Strings.Get("WaitingForEvo");
        SpeedText = "0 km/h";
        GearText = "N";
        DetailText = string.Empty;
    }

    // ---- bindable state -------------------------------------------------

    private bool _isLocked = true;
    public bool IsLocked
    {
        get => _isLocked;
        set { if (SetField(ref _isLocked, value)) { OnPropertyChanged(nameof(LockButtonText)); OnPropertyChanged(nameof(HintText)); } }
    }

    public string LockButtonText => Strings.Get(IsLocked ? "Locked" : "Unlock");
    public string HintText => Strings.Get(IsLocked ? "HintLocked" : "HintUnlocked");

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
    public ICommand ToggleLockCommand { get; }

    // ---- lifecycle (called by the view) ----------------------------------

    public void Start()
    {
        _lastTick = _clock.UtcNow;
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

        var s = _telemetry.TryRead();
        if (s is null)
        {
            IsLive = false;
            StatusText = Strings.Get(_telemetry.GameRunning ? "EvoNoShm" : "WaitingForEvo");
            SpeedText = "0 km/h";
            return;
        }

        IsLive = true;
        StatusText = $"EVO live · pkt {s.PacketId}";
        SpeedText = $"{s.SpeedKmh:0} km/h";
        GearText = TelemetryConventions.GearDisplay(s.Gear);
        DetailText = $"rpm {s.Rpm} · thr {s.Gas:0%} · brk {s.Brake:0%}";

        // Meter runs while the passenger is aboard.
        if (_job.Phase == JobPhase.EnRoute && dt is > 0 and < 1)
        {
            _job.DistanceKm += Math.Max(0, s.SpeedKmh) / 3600.0 * dt;
            _job.Fare = TaxiFare.ComputeFare(_job.DistanceKm, _fares);
            RefreshJob();
        }
    }

    // ---- job commands ------------------------------------------------------

    private void NewJob()
    {
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
        _shift.RecordCompleted(_job);
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
        FareText = $"€{_job.Total:0.00}";
        ShiftText = Strings.Format("ShiftSummary", _shift.JobsDone, _shift.Earned);
        ((RelayCommand)PickupCommand).RaiseCanExecuteChanged();
        ((RelayCommand)DropoffCommand).RaiseCanExecuteChanged();
    }

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
        _telemetry.Dispose();
    }
}
