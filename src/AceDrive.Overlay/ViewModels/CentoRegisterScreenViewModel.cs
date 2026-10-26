using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The start-line registration check (PORTPLAN item 49, port of Unity
/// RegisterScreen): the keypad the player uses to pick the number the club
/// issued on the event page. The page owns only presentation and the check's
/// own buffer; ACCEPT starts the race, the second wrong number closes the
/// entry, and BACK gives the entry up.
///
/// The keypad is a selection: every digit that is part of the number picked so
/// far paints teal and the line above the keys repeats it, so the player reads
/// the number off the pad instead of guessing what went in. Each key answers to
/// its spoken word and numeral too, so the number can be picked by voice.
///
/// Unity disables the keypad and auto-resets two seconds after the refusal;
/// the port keeps the refusal on the page and leaves BACK as the way out, so a
/// failed check never traps the player behind a page they cannot dismiss.
/// </summary>
public sealed class CentoRegisterScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    // Unity's keypad is the ten digits; the view lays the keys out in order.
    private const int KeypadDigits = 10;

    private readonly CentoRegistrationCheck _check;
    private readonly Action _startRace;
    private readonly Action _abandon;
    private readonly VoiceBeat _beat;
    private string _feedback = string.Empty;

    public CentoRegisterScreenViewModel(
        CentoRegistrationCheck check, Action startRace, Action abandon, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(startRace);
        ArgumentNullException.ThrowIfNull(abandon);
        ArgumentNullException.ThrowIfNull(beat);
        _check = check;
        _startRace = startRace;
        _abandon = abandon;
        _beat = beat;
        Keys = BuildKeys();
        DigitCommand = new ParameterizedCommand<int>(Submit);
        ContinueCommand = new RelayCommand(Continue);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>The keypad's keys, in the order the screen shows them.</summary>
    public IReadOnlyList<CentoRegisterKey> Keys { get; private set; }

    /// <summary>The page's fixed prompt (content, never resx in the View).</summary>
    public string PromptLine { get; } = Strings.Get("CentoRegisterPrompt");

    /// <summary>
    /// "SELECTED: 1 2" for the digits picked so far, or the dash placeholder
    /// while no digit is in. A judged number stays up until the next digit
    /// starts the new attempt, so the player always sees what they picked.
    /// </summary>
    public string SelectedLine
    {
        get
        {
            string selected = _check.EnteredDigits.Count == 0
                ? Strings.Get("CentoRegisterNoDigits")
                : string.Join(' ', _check.EnteredDigits);
            return Strings.Format("CentoRegisterSelected", selected);
        }
    }

    /// <summary>The refusal line, empty while the player is still typing.</summary>
    public string FeedbackLine
    {
        get => _feedback;
        private set
        {
            if (SetField(ref _feedback, value))
            {
                OnPropertyChanged(nameof(HasFeedback));
            }
        }
    }

    public bool HasFeedback => _feedback.Length > 0;

    /// <summary>True until the number is accepted or the entry closes.</summary>
    public bool CanType => !_check.IsSolved && !_check.IsClosed;

    /// <summary>True once the right number was typed: CONTINUE starts the race.</summary>
    public bool ShowContinue => _check.IsSolved;

    /// <summary>True when too many wrong numbers closed the entry for good.</summary>
    public bool IsClosed => _check.IsClosed;

    public ICommand DigitCommand { get; }

    public ICommand ContinueCommand { get; }

    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash
    {
        get => _continueFlash;
        private set => SetField(ref _continueFlash, value);
    }
    private bool _continueFlash;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // One picked digit: the check answers pending/right/wrong/closed and the
    // page paints that answer plus the keys the selection covers. A solved or
    // closed entry ignores the keypad (the buttons are disabled too; this
    // guards a stale command).
    private void Submit(int digit)
    {
        if (!CanType)
        {
            return;
        }

        CentoRegistrationResult result = _check.Submit(digit);
        FeedbackLine = result switch
        {
            CentoRegistrationResult.WrongAttempt => Strings.Get("CentoRegisterWrong"),
            CentoRegistrationResult.Closed => Strings.Get("CentoRegisterClosed"),
            _ => string.Empty,
        };
        Keys = BuildKeys();
        OnPropertyChanged(nameof(Keys));
        OnPropertyChanged(nameof(SelectedLine));
        OnPropertyChanged(nameof(CanType));
        OnPropertyChanged(nameof(ShowContinue));
        OnPropertyChanged(nameof(IsClosed));
        RebuildVoice();
    }

    private void Continue()
    {
        if (!_check.IsSolved)
        {
            return;
        }

        ContinueFlash = false;
        _startRace();
    }

    private void Back()
    {
        BackFlash = false;
        _abandon();
    }

    // The keypad in Unity's 0..9 order, each key marked when the digit is part
    // of the number picked so far (the view paints those teal).
    private CentoRegisterKey[] BuildKeys()
    {
        var keys = new CentoRegisterKey[KeypadDigits];
        for (int digit = 0; digit < keys.Length; digit++)
        {
            keys[digit] = new CentoRegisterKey(digit, IsSelected(digit));
        }
        return keys;
    }

    private bool IsSelected(int digit)
    {
        foreach (int entered in _check.EnteredDigits)
        {
            if (entered == digit)
            {
                return true;
            }
        }
        return false;
    }

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>
        {
            [Strings.Get("DeliveryBack")] = FlashThenBack,
        };
        if (_check.IsSolved)
        {
            actions[Strings.Get("ReceiptContinue")] = FlashThenContinue;
        }
        // The keypad answers by voice like every other button (the spoken digits
        // in CentoRegisterVoice). The word and the numeral share one delegate,
        // so the router's repeat mute treats them as the same tap; only a live
        // keypad registers them, so a solved or closed entry cannot take a
        // stray digit.
        if (CanType)
        {
            foreach (CentoRegisterKey key in Keys)
            {
                int digit = key.Digit;
                Action submit = () => Submit(digit);
                foreach (string keyword in CentoRegisterVoice.KeywordsFor(digit))
                {
                    actions[keyword] = submit;
                }
            }
        }
        VoiceActions = actions;
    }

    private void FlashThenContinue()
    {
        ContinueFlash = true;
        _beat.Defer(Continue);
    }

    private void FlashThenBack()
    {
        BackFlash = true;
        _beat.Defer(Back);
    }
}
