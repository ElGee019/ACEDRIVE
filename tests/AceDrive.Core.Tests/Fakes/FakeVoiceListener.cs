using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeVoiceListener(bool available = true) : IVoiceListener
{
    public bool IsAvailable { get; } = available;
    public bool Started { get; private set; }

    /// <summary>The production listener's constrained-decoder switch.</summary>
    public bool WakeListening { get; set; }

    public event EventHandler<VoiceTextEventArgs>? Heard;

    public event EventHandler<VoiceTextEventArgs>? WakeHeard;

    /// <summary>Speaks a full-vocabulary fragment (the command decoder).</summary>
    public void Fire(string text, bool isFinal = false) =>
        Heard?.Invoke(this, new VoiceTextEventArgs(text, isFinal));

    /// <summary>Speaks a constrained-decoder fragment (the wake grammar).</summary>
    public void FireWake(string text, bool isFinal = false) =>
        WakeHeard?.Invoke(this, new VoiceTextEventArgs(text, isFinal));

    public void StartListening() => Started = true;
    public void StopListening() { }
    public void Dispose() { }
}
