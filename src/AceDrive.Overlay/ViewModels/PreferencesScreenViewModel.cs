using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// First page of the introduction (before the name + address form): the player
/// picks the distance unit and the UI language once, so the HUD and every later
/// screen read in their language. The settings entry reuses this page; it swaps
/// the action label to BACK, returns to the menu, answers the English voice
/// label, and adds the QUIT and destructive reset game actions (both absent on
/// the first run). The first-run page stays mouse-only like the other setup
/// pages.
/// The option labels are language-independent on purpose — "km"/"mi" symbols
/// and endonyms ("English"/"Nederlands") — so switching the language never has
/// to rebuild the dropdowns; only the page copy below re-reads.
/// </summary>
public sealed class PreferencesScreenViewModel : ViewModelBase, IVoiceScreen
{
    private const string ContinueLabelKey = "PreferencesContinue";
    private const string QuitLabelKey = "SettingsQuit";
    private const string CallLabelKey = "PreferencesCall";
    private const string CallPromptKey = "PreferencesCallPrompt";
    private const string ResetLabelKey = "SettingsResetGame";
    private const string ResetConfirmLabelKey = "SettingsResetConfirm";
    private const string ResetWarningKey = "SettingsResetWarning";
    private const string ResetFailedKey = "SettingsResetFailed";
    private const string ResetFailedInUseKey = "SettingsResetFailedInUse";
    private const string ResetFailedDeniedKey = "SettingsResetFailedDenied";
    private const string ResetFailedRestartKey = "SettingsResetFailedRestart";

    private readonly Action<PlayerPreferences> _changed;
    private readonly Action _done;
    private readonly ICallBinder? _callBinder;
    private readonly string _doneLabelKey;
    // Null on the first run: the walk has no session to quit and CONTINUE
    // already advances it.
    private readonly Action? _quit;
    // Null on the first run: there is no career to wipe yet.
    private readonly Func<GameResetFailure>? _resetGame;

    private PreferenceOption<MileageUnit> _unit;
    private PreferenceOption<UiLanguage> _language;
    // Null until the player assigns their own input: the shipped chord is then
    // what both this button and the menu read.
    private CallBinding? _call;

    // Bindable copy, not {loc:Loc}: this page IS the language selector, so
    // its own labels must re-read the moment the choice changes.
    private string _title = Strings.Get("PreferencesTitle");
    private string _unitsLabel = Strings.Get("PreferencesUnits");
    private string _languageLabel = Strings.Get("PreferencesLanguage");
    private string _continueLabel = Strings.Get(ContinueLabelKey);
    private string _quitLabel = Strings.Get(QuitLabelKey);
    private string _callLabel = string.Empty;
    private string _resetLabel = Strings.Get(ResetLabelKey);
    private string _resetWarning = Strings.Get(ResetWarningKey);
    private string _resetStatus = string.Empty;
    private bool _assigningCall;
    private bool _confirmingReset;
    // The last failure, kept so a language change can re-read the same
    // sentence in the new language instead of dropping or freezing it.
    private GameResetFailure _resetFailure = GameResetFailure.None;

    public PreferencesScreenViewModel(
        PlayerPreferences current,
        Action<PlayerPreferences> changed,
        Action done,
        string doneLabelKey = ContinueLabelKey,
        string? voiceDoneLabel = null,
        Func<GameResetFailure>? resetGame = null,
        Action? quit = null,
        ICallBinder? callBinder = null)
    {
        _changed = changed;
        _done = done;
        _doneLabelKey = doneLabelKey;
        _quit = quit;
        _resetGame = resetGame;
        _callBinder = callBinder;
        _call = current.Call;
        _continueLabel = Strings.Get(doneLabelKey);
        _callLabel = CallText();
        var voice = new Dictionary<string, Action>(StringComparer.Ordinal);
        if (voiceDoneLabel is not null)
        {
            voice[voiceDoneLabel] = () => _done();
        }
        if (quit is not null)
        {
            // QUIT answers the English word whatever the page prints (the model
            // is English, HudVoiceKeywords' rule). RESET GAME stays mouse-only:
            // a misheard word must not wipe a career, even behind its confirm.
            voice["quit"] = Quit;
        }
        VoiceActions = voice;
        UnitOptions =
        [
            new(MileageUnit.Kilometres, "km"),
            new(MileageUnit.Miles, "mi"),
        ];
        LanguageOptions =
        [
            new(UiLanguage.English, "English"),
            new(UiLanguage.Dutch, "Nederlands"),
        ];
        _unit = UnitOptions.FirstOrDefault(o => o.Value == current.Units) ?? UnitOptions[0];
        _language = LanguageOptions.FirstOrDefault(o => o.Value == current.Language) ?? LanguageOptions[0];
        ContinueCommand = new RelayCommand(LeaveToDone);
        QuitCommand = new RelayCommand(LeaveToQuit);
        ResetCommand = new RelayCommand(RequestReset);
        AssignCallCommand = new RelayCommand(AssignCall);
    }

    // Page copy as bindable text, not {loc:Loc}: this page IS the language
    // selector, so its own labels must re-read the moment the choice changes.
    public string Title { get => _title; private set => SetField(ref _title, value); }
    public string UnitsLabel { get => _unitsLabel; private set => SetField(ref _unitsLabel, value); }
    public string LanguageLabel { get => _languageLabel; private set => SetField(ref _languageLabel, value); }
    public string ContinueLabel { get => _continueLabel; private set => SetField(ref _continueLabel, value); }

    /// <summary>The settings entry can leave the session; the first run cannot.</summary>
    public bool CanQuit => _quit is not null;

    /// <summary>QUIT, re-read on a language change like the rest of the page.</summary>
    public string QuitLabel { get => _quitLabel; private set => SetField(ref _quitLabel, value); }

    /// <summary>The settings entry has a career to wipe; the first run does not.</summary>
    public bool CanReset => _resetGame is not null;

    /// <summary>First tap only warns; the second tap wipes and restarts.</summary>
    public bool IsConfirmingReset
    {
        get => _confirmingReset;
        private set
        {
            if (!SetField(ref _confirmingReset, value)) return;
            ResetLabel = Strings.Get(value ? ResetConfirmLabelKey : ResetLabelKey);
        }
    }

    /// <summary>RESET GAME, or the confirmation label after the first tap.</summary>
    public string ResetLabel { get => _resetLabel; private set => SetField(ref _resetLabel, value); }

    /// <summary>The warning shown between the two taps.</summary>
    public string ResetWarning { get => _resetWarning; private set => SetField(ref _resetWarning, value); }

    /// <summary>Failure line after a wipe/restart that could not complete.</summary>
    public string ResetStatus { get => _resetStatus; private set => SetField(ref _resetStatus, value); }

    public IReadOnlyList<PreferenceOption<MileageUnit>> UnitOptions { get; }
    public IReadOnlyList<PreferenceOption<UiLanguage>> LanguageOptions { get; }

    public PreferenceOption<MileageUnit> SelectedUnit
    {
        get => _unit;
        set
        {
            if (SetField(ref _unit, value)) OnSelectionChanged();
        }
    }

    public PreferenceOption<UiLanguage> SelectedLanguage
    {
        get => _language;
        set
        {
            if (SetField(ref _language, value)) OnSelectionChanged();
        }
    }

    public ICommand ContinueCommand { get; }

    /// <summary>QUIT; inert on the first-run page, where it is hidden.</summary>
    public ICommand QuitCommand { get; }

    /// <summary>RESET GAME; inert on the first-run page, where it is hidden.</summary>
    public ICommand ResetCommand { get; }

    /// <summary>
    /// ASSIGN CALL: puts the platform in listening mode for the next input. A
    /// second tap cancels instead, so a page that is not seeing the player's
    /// wheel can be backed out of without leaving the listener running.
    /// </summary>
    public ICommand AssignCallCommand { get; }

    /// <summary>The assigned call as the button prints it, or the listening prompt.</summary>
    public string CallLabel { get => _callLabel; private set => SetField(ref _callLabel, value); }

    /// <summary>True while the page waits for an input to assign.</summary>
    public bool IsAssigningCall { get => _assigningCall; private set => SetField(ref _assigningCall, value); }

    /// <summary>A listener exists: the first-run page has one too, and only a
    /// headless test page passes none, where the row stays hidden.</summary>
    public bool CanAssignCall => _callBinder is not null;

    /// <summary>
    /// Spoken label -&gt; action: only the settings reuse registers anything (the
    /// BACK passed in, and the English "quit"); the first-run page is mouse-only,
    /// and RESET GAME never answers the mic.
    /// </summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    private void OnSelectionChanged()
    {
        // Push first: MainViewModel applies the UI culture, so the labels
        // re-read below already answer in the newly picked language.
        PushPreferences();
        Title = Strings.Get("PreferencesTitle");
        UnitsLabel = Strings.Get("PreferencesUnits");
        LanguageLabel = Strings.Get("PreferencesLanguage");
        ContinueLabel = Strings.Get(_doneLabelKey);
        QuitLabel = Strings.Get(QuitLabelKey);
        CallLabel = IsAssigningCall ? Strings.Get(CallPromptKey) : CallText();
        ResetLabel = Strings.Get(
            IsConfirmingReset ? ResetConfirmLabelKey : ResetLabelKey);
        ResetWarning = Strings.Get(ResetWarningKey);
        if (_resetFailure.Kind != GameResetFailureKind.None)
        {
            ResetStatus = DescribeReset(_resetFailure);
        }
    }

    // The call rides along on every write: a unit or language change must not
    // drop the input the player assigned on this very page.
    private void PushPreferences() =>
        _changed(new PlayerPreferences(_unit.Value, _language.Value) { Call = _call });

    // The button names its job, not the current binding: the assigned input is
    // read back on the menu page ("you can also press …"), which is the line
    // the player sees from the driver's seat. While listening, the prompt
    // replaces it so the page says what it is waiting for.
    private static string CallText() => Strings.Get(CallLabelKey);

    private void AssignCall()
    {
        if (_callBinder is null)
        {
            return;
        }

        if (IsAssigningCall)
        {
            _callBinder.CancelCapture();
            return;
        }

        IsAssigningCall = true;
        CallLabel = Strings.Get(CallPromptKey);
        _callBinder.BeginCapture(OnCallCaptured);
    }

    // Escape and a second tap answer null: the button goes back to the input
    // that is still armed instead of keeping the prompt up for good.
    private void OnCallCaptured(CallBinding? binding)
    {
        IsAssigningCall = false;
        if (binding is null)
        {
            CallLabel = CallText();
            return;
        }

        _call = binding;
        CallLabel = CallText();
        PushPreferences();
    }

    // Leaving the page while it waits for an input would leave the call
    // listener disarmed (BeginCapture drops the armed hotkey), so both ways out
    // cancel first.
    private void LeaveToDone()
    {
        _callBinder?.CancelCapture();
        _done();
    }

    private void LeaveToQuit()
    {
        _callBinder?.CancelCapture();
        Quit();
    }

    // QUIT closes the panel, which is the app's one exit path: the window's
    // close handlers still bank the session, so this is not a hard exit.
    private void Quit() => _quit?.Invoke();

    // Two taps: the first reveals the warning and asks again, so a stray click
    // on the settings page never deletes a career. The second wipes the save
    // files and restarts the overlay (which ends this process in production).
    private void RequestReset()
    {
        if (_resetGame is null) return;
        _resetFailure = GameResetFailure.None;
        ResetStatus = string.Empty;
        if (!IsConfirmingReset)
        {
            IsConfirmingReset = true;
            return;
        }

        GameResetFailure failure = _resetGame();
        // None means the replacement process is starting and this one is about
        // to end; painting a status line here would only flash for a frame.
        if (failure.Kind == GameResetFailureKind.None) return;

        _resetFailure = failure;
        IsConfirmingReset = false;
        ResetStatus = DescribeReset(failure);
    }

    // WHY three keys: a file another program holds open, a delete Windows
    // refused and a restart that would not spawn are fixed by three different
    // actions, so the single "close AceDrive and try again" line pointed the
    // player the wrong way in two of them.
    private static string DescribeReset(GameResetFailure failure) => failure.Kind switch
    {
        GameResetFailureKind.SaveInUse when failure.File is not null =>
            Strings.Format(ResetFailedInUseKey, failure.File),
        GameResetFailureKind.SaveDenied when failure.File is not null =>
            Strings.Format(ResetFailedDeniedKey, failure.File),
        GameResetFailureKind.Restart => Strings.Get(ResetFailedRestartKey),
        _ => Strings.Get(ResetFailedKey),
    };
}
