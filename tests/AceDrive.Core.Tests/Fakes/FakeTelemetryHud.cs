using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Captures the one snapshot MainViewModel pushes per poll.</summary>
internal sealed class FakeTelemetryHud : ITelemetryHud
{
    public int Calls { get; private set; }
    public TelemetrySample? Sample { get; private set; }
    public double? OdometerKm { get; private set; }
    public bool GameRunning { get; private set; }
    public bool WrongCar { get; private set; }
    public PlayerPreferences? Preferences { get; private set; }
    public PointOfInterest? Destination { get; private set; }
    public HudDestinationCopy? Copy { get; private set; }
    public string? HintKey => Copy?.DriveKey;
    public HudJobGuidance? JobGuidance { get; private set; }
    public string? Notice { get; private set; }
    public TimeSpan? NoticeDuration { get; private set; }
    public bool AtDestination { get; set; }
    public bool Revealed { get; private set; }
    public int EnterRequests { get; private set; }
    public int JobEnterRequests { get; private set; }

    public event EventHandler? EnterRequested;
    public event EventHandler? JobEnterRequested;

    public void Reveal() => Revealed = true;

    public void RaiseEnter() => EnterRequested?.Invoke(this, EventArgs.Empty);

    public void RaiseJobEnter()
    {
        JobEnterRequests++;
        JobEnterRequested?.Invoke(this, EventArgs.Empty);
    }

    public void RequestEnter()
    {
        EnterRequests++;
        EnterRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Update(
        TelemetrySample? sample, double? odometerKm, bool gameRunning,
        bool wrongCar = false)
    {
        Calls++;
        Sample = sample;
        OdometerKm = odometerKm;
        GameRunning = gameRunning;
        WrongCar = wrongCar;
    }

    public void ApplyPreferences(PlayerPreferences preferences) => Preferences = preferences;

    public void SetDestination(PointOfInterest? destination, HudDestinationCopy? copy = null)
    {
        Destination = destination;
        Copy = copy;
    }

    public void SetJobGuidance(HudJobGuidance? guidance) => JobGuidance = guidance;

    public void ShowNotice(string text, TimeSpan duration)
    {
        Notice = text;
        NoticeDuration = duration;
    }
}
