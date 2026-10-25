using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// First page of the introduction (before the name + address form): the player
/// picks the distance unit and the UI language once, so the HUD and every later
/// screen read in their language. The settings entry reuses this page; it swaps
/// the action label to BACK, returns to the menu, answers the English voice
/// label, and adds the destructive reset game action (absent on the first run).
/// The first-run page stays mouse-only like the other setup pages.
/// The option labels are language-independent on purpose — "km"/"mi" symbols
/// and endonyms ("English"/"Nederlands") — so switching the language never has
/// to rebuild the dropdowns; only the page copy below re-reads.
/// </summary>
public sealed class PreferencesScreenViewModel : ViewModelBase, IVoiceScreen
{
    private const string ContinueLabelKey = "PreferencesContinue";
    private const string ResetLabelKey = "SettingsResetGame";
    private const string ResetConfirmLabelKey = "SettingsResetConfirm";
    private const string ResetWarningKey = "SettingsResetWarning";
    private const string ResetFailedKey = "SettingsResetFailed";
    private const string ResetFailedInUseKey = "SettingsResetFailedInUse";
    private const string ResetFailedDeniedKey = "SettingsResetFailedDenied";
    private const string ResetFailedRestartKey = "SettingsResetFailedRestart";

    private readonly Action<PlayerPreferences> _changed;
    private readonly Action _done;
    private readonly string _doneLabelKey;
    // Null on the first run: there is no career to wipe yet.
    private readonly Func<GameResetFailure>? _resetGame;

    private PreferenceOption<MileageUnit> _unit;
    private PreferenceOption<UiLanguage> _language;

    // Bindable copy, not {loc:Loc}: this page IS the language selector, so
    // its own labels must re-read the moment the choice changes.
    private string _title = Strings.Get("PreferencesTitle");
    private string _unitsLabel = Strings.Get("PreferencesUnits");
    private string _languageLabel = Strings.Get("PreferencesLanguage");
    private string _continueLabel = Strings.Get(ContinueLabelKey);
    private string _resetLabel = Strings.Get(ResetLabelKey);
    private string _resetWarning = Strings.Get(ResetWarningKey);
    private string _resetStatus = string.Empty;
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
        Func<GameResetFailure>? resetGame = null)
    {
        _changed = changed;
        _done = done;
        _doneLabelKey = doneLabelKey;
        _resetGame = resetGame;
        _continueLabel = Strings.Get(doneLabelKey);
        var voice = new Dictionary<string, Action>(StringComparer.Ordinal);
        if (voiceDoneLabel is not null)
        {
            voice[voiceDoneLabel] = () => _done();
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
        ContinueCommand = new RelayCommand(() => _done());
        ResetCommand = new RelayCommand(RequestReset);
    }

    // Page copy as bindable text, not {loc:Loc}: this page IS the language
    // selector, so its own labels must re-read the moment the choice changes.
    public string Title { get => _title; private set => SetField(ref _title, value); }
    public string UnitsLabel { get => _unitsLabel; private set => SetField(ref _unitsLabel, value); }
    public string LanguageLabel { get => _languageLabel; private set => SetField(ref _languageLabel, value); }
    public string ContinueLabel { get => _continueLabel; private set => SetField(ref _continueLabel, value); }

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

    /// <summary>RESET GAME; inert on the first-run page, where it is hidden.</summary>
    public ICommand ResetCommand { get; }

    /// <summary>Spoken label -&gt; action; only the settings reuse registers BACK.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    private void OnSelectionChanged()
    {
        // Push first: MainViewModel applies the UI culture, so the labels
        // re-read below already answer in the newly picked language.
        _changed(new PlayerPreferences(_unit.Value, _language.Value));
        Title = Strings.Get("PreferencesTitle");
        UnitsLabel = Strings.Get("PreferencesUnits");
        LanguageLabel = Strings.Get("PreferencesLanguage");
        ContinueLabel = Strings.Get(_doneLabelKey);
        ResetLabel = Strings.Get(
            IsConfirmingReset ? ResetConfirmLabelKey : ResetLabelKey);
        ResetWarning = Strings.Get(ResetWarningKey);
        if (_resetFailure.Kind != GameResetFailureKind.None)
        {
            ResetStatus = DescribeReset(_resetFailure);
        }
    }

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
