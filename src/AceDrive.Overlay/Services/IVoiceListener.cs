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

    /// <summary>Every fragment of the full vocabulary decoder (commands).</summary>
    event EventHandler<VoiceTextEventArgs>? Heard;

    /// <summary>
    /// Fragments of the constrained wake decoder: only the wake phrases or the
    /// unknown token, so a hit here names a wake word the engine noise could
    /// not swallow. Fed only while <see cref="WakeListening"/> is on.
    /// </summary>
    event EventHandler<VoiceTextEventArgs>? WakeHeard;

    /// <summary>
    /// True while the panel is down, i.e. while only a wake word can be
    /// useful: the constrained decoder is fed alongside the full one, never
    /// instead of it, so the HUD's own keywords keep answering mid-drive.
    /// Setting it starts the constrained decoder from a clean state, because
    /// the audio it missed is not its utterance.
    /// </summary>
    bool WakeListening { get; set; }

    void StartListening();
    void StopListening();
}
