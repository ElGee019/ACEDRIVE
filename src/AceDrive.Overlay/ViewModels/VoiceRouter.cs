using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Voice dispatch: the wake word, the companion HUD's ENTER button, and the
/// label match that taps the current screen's button. Owns the utterance-level
/// mute state so one utterance fires one button, and nothing about screens or
/// HWNDs beyond the <see cref="IVoiceHost"/> slice it is handed.
/// </summary>
public sealed class VoiceRouter
{
    // The companion HUD sits outside the main screen stack, so its ENTER
    // button answers even while the overlay is hidden for driving.
    // The HUD ENTER button answers to the one word the English mic model hears
    // reliably, whatever the UI language shows on the button.
    private static readonly string[] EnterKeywords = ["enter"];
    private const double WakeCooldownSec = 3.0;

    private readonly IVoiceHost _host;
    private readonly IClock _clock;
    private readonly ILog _log;
    private readonly ITelemetryHud? _hud;
    private DateTime _lastWake = DateTime.MinValue;
    private string? _mutedLabel;
    private Action? _mutedAction;
    private string _mutedText = string.Empty;

    public VoiceRouter(IVoiceHost host, IClock clock, ILog log, ITelemetryHud? hud)
    {
        _host = host;
        _clock = clock;
        _log = log;
        _hud = hud;
    }

    /// <summary>
    /// Forgets the last fired tap. Hiding the panel is a hard context break:
    /// the next utterance is a fresh request, not a repeat of the old one.
    /// </summary>
    public void ResetMute()
    {
        _mutedLabel = null;
        _mutedAction = null;
        _mutedText = string.Empty;
    }

    public void Handle(string text, bool isFinal)
    {
        // Diagnostic: one line per finished utterance. Non-English names
        // ("Cento Miglia") arrive as whatever the English model believes it
        // heard; reading the real transcripts from the log lets us extend
        // the keyword tables from data instead of guesses.
        if (isFinal) _log.LogInfo($"voice heard: {text}");

        // Finals only: one utterance must not enter twice.
        if (isFinal && TryEnterPoi(text))
        {
            return;
        }

        if (!_host.IsShown)
        {
            string brand = _host.CarBrand;
            if (VoiceMatch.IsWake(text, brand, AliasesFor(brand), _host.Content.Confusions, isFinal))
            {
                DateTime now = _clock.UtcNow;
                if ((now - _lastWake).TotalSeconds < WakeCooldownSec) return;
                _lastWake = now;
                _log.LogInfo($"wake heard: {text}");
                _host.Show();
            }

            return;
        }

        if (_host.CurrentScreen is not IVoiceScreen screen) return;
        string? label = VoiceMatch.MatchLabel(text, screen.VoiceActions.Keys, _host.Content.Confusions);
        if (label is null)
        {
            // A failed attempt is still the user being present. The logged
            // 2026-10-13 session hid mid-retry ("open your career" x3, then
            // the 15 s window expired and the next phrase woke to home
            // instead of pressing the button). Unmatched FINAL transcripts
            // restart the idle window; partials do not, so a background
            // voice that never finalizes keeps the old hide timing.
            if (isFinal) _host.RecordActivity();
            return;
        }
        // Voice control counts as user input: same idle-reset as a click.
        // Repeats of the same tap still count as presence (restarting the
        // 15 s window) even though the repeat itself stays muted below.
        _host.RecordActivity();
        // Mute repeats of the same tap until the utterance's final fragment.
        // One utterance can reach a button under two different keys (a
        // partial hearing "where is" alias, the final "Where is Willy"
        // label), so the shared action delegate counts as the same tap too.
        // WHY the utterance text and the fired label's word run join the
        // mute: a beat-deferred page change can hand the FINAL fragment to
        // the new screen, where the same words map to a different label and
        // delegate. Logged 2026-10-14 18:37: "we continue" closed the
        // birthday dialogue and its final re-scored to "the continue" one
        // second later — already on the control page, where bare
        // "continue" is a taxi keyword: the tail tapped the button and the
        // shift started with nobody asking for it. Exact text missed it
        // (leading word rewritten), the label's word run survives:
        // fragments answering the fired label are the same utterance,
        // whichever page is now on screen.
        Action action = screen.VoiceActions[label];
        string heard = VoiceMatch.Normalize(text);
        if (label == _mutedLabel || action == _mutedAction || heard == _mutedText
            || (_mutedLabel is not null && VoiceMatch.AnswersText(_mutedLabel, heard)))
        {
            if (isFinal)
            {
                _mutedLabel = null;
                _mutedAction = null;
                _mutedText = string.Empty;
            }
            return;
        }

        _mutedLabel = label;
        _mutedAction = action;
        _mutedText = heard;
        _log.LogInfo($"voice button: {text}");
        action.Invoke();
    }

    // HUD arrival voice tap: only while the button is up, and only a finished
    // utterance. The HUD owns the teal acknowledgment and the half-second
    // wait; the host still owns what entering means.
    private bool TryEnterPoi(string text)
    {
        if (_hud is not { AtDestination: true })
        {
            return false;
        }
        if (VoiceMatch.MatchLabel(text, EnterKeywords, _host.Content.Confusions) is null)
        {
            return false;
        }

        _host.RecordActivity();
        _log.LogInfo($"voice button: {text}");
        _hud.RequestEnter();
        return true;
    }

    private string[] AliasesFor(string brand)
    {
        OnboardingContent content = _host.Content;
        if (content.Aliases.TryGetValue(brand, out string[]? hit)) return hit;
        return content.Aliases.TryGetValue(VoiceMatch.Normalize(brand), out string[]? norm) ? norm : [];
    }
}
