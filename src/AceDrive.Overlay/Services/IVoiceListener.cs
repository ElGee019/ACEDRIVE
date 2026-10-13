namespace AceDrive.Overlay.Services;

/// <summary>One STT fragment. Final ends the utterance; partials precede it.</summary>
public sealed class VoiceTextEventArgs(string text, bool isFinal) : EventArgs
{
    public string Text { get; } = text;
    public bool IsFinal { get; } = isFinal;
}

/// <summary>
/// Mic-to-text behind an interface. Implementations never throw out of the
/// audio thread; they degrade to silence and log. Fakes drive voice flows
/// in tests.
/// </summary>
public interface IVoiceListener : IDisposable
{
    bool IsAvailable { get; }
    event EventHandler<VoiceTextEventArgs>? Heard;
    void StartListening();
    void StopListening();
}
