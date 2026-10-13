using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeVoiceListener(bool available = true) : IVoiceListener
{
    public bool IsAvailable { get; } = available;
    public bool Started { get; private set; }
    public event EventHandler<VoiceTextEventArgs>? Heard;
    public void Fire(string text, bool isFinal = false) =>
        Heard?.Invoke(this, new VoiceTextEventArgs(text, isFinal));
    public void StartListening() => Started = true;
    public void StopListening() { }
    public void Dispose() { }
}
