namespace AceDrive.Overlay.Services;

/// <summary>No-mic stand-in: never fires. Used when no model is configured and in tests.</summary>
public sealed class NullVoiceListener : IVoiceListener
{
    public bool IsAvailable => false;
    public event EventHandler<VoiceTextEventArgs>? Heard
    {
        add { }
        remove { }
    }

    public void StartListening() { }
    public void StopListening() { }
    public void Dispose() { }
}
