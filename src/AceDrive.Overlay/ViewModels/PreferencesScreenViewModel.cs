using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// First page of the introduction (before the name + address form): the player
/// picks the distance unit and the UI language once, so the HUD and every later
/// screen read in their language. Mouse-only like the other setup pages.
/// The option labels are language-independent on purpose — "km"/"mi" symbols
/// and endonyms ("English"/"Nederlands") — so switching the language never has
/// to rebuild the dropdowns; only the page copy below re-reads.
/// </summary>
public sealed class PreferencesScreenViewModel : ViewModelBase
{
    private readonly Action<PlayerPreferences> _changed;
    private readonly Action _done;

    private PreferenceOption<MileageUnit> _unit;
    private PreferenceOption<UiLanguage> _language;

    // Bindable copy, not {loc:Loc}: this page IS the language selector, so
    // its own labels must re-read the moment the choice changes.
    private string _title = Strings.Get("PreferencesTitle");
    private string _unitsLabel = Strings.Get("PreferencesUnits");
    private string _languageLabel = Strings.Get("PreferencesLanguage");
    private string _continueLabel = Strings.Get("PreferencesContinue");

    public PreferencesScreenViewModel(
        PlayerPreferences current,
        Action<PlayerPreferences> changed,
        Action done)
    {
        _changed = changed;
        _done = done;
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
    }

    // Page copy as bindable text, not {loc:Loc}: this page IS the language
    // selector, so its own labels must re-read the moment the choice changes.
    public string Title { get => _title; private set => SetField(ref _title, value); }
    public string UnitsLabel { get => _unitsLabel; private set => SetField(ref _unitsLabel, value); }
    public string LanguageLabel { get => _languageLabel; private set => SetField(ref _languageLabel, value); }
    public string ContinueLabel { get => _continueLabel; private set => SetField(ref _continueLabel, value); }

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

    private void OnSelectionChanged()
    {
        // Push first: MainViewModel applies the UI culture, so the labels
        // re-read below already answer in the newly picked language.
        _changed(new PlayerPreferences(_unit.Value, _language.Value));
        Title = Strings.Get("PreferencesTitle");
        UnitsLabel = Strings.Get("PreferencesUnits");
        LanguageLabel = Strings.Get("PreferencesLanguage");
        ContinueLabel = Strings.Get("PreferencesContinue");
    }
}
