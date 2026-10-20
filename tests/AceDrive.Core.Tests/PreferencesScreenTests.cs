using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>First-run page: the unit + language choices and CONTINUE.</summary>
public sealed class PreferencesScreenTests
{
    private static PreferencesScreenViewModel Create(
        List<PlayerPreferences> changed, Action done,
        PlayerPreferences? current = null) =>
        new(current ?? PlayerPreferences.Default, changed.Add, done);

    [Fact]
    public void Defaults_AreKilometresAndEnglish()
    {
        var vm = Create([], () => { });

        Assert.Equal(MileageUnit.Kilometres, vm.SelectedUnit.Value);
        Assert.Equal(UiLanguage.English, vm.SelectedLanguage.Value);
        // Labels stay language-independent so a switch never rebuilds them.
        Assert.Equal(["km", "mi"], vm.UnitOptions.Select(o => o.Label));
        Assert.Equal(["English", "Nederlands"], vm.LanguageOptions.Select(o => o.Label));
    }

    [Fact]
    public void SelectingMiles_PushesTheNewPreferences()
    {
        var changed = new List<PlayerPreferences>();
        var vm = Create(changed, () => { });

        vm.SelectedUnit = vm.UnitOptions[1];

        Assert.Equal(
            new PlayerPreferences(MileageUnit.Miles, UiLanguage.English),
            Assert.Single(changed));
    }

    [Fact]
    public void SelectingDutch_PushesTheNewLanguage()
    {
        var changed = new List<PlayerPreferences>();
        var vm = Create(changed, () => { });

        vm.SelectedLanguage = vm.LanguageOptions[1];

        Assert.Equal(
            new PlayerPreferences(MileageUnit.Kilometres, UiLanguage.Dutch),
            Assert.Single(changed));
    }

    [Fact]
    public void SavedPreferences_PreselectTheOptions()
    {
        var vm = Create([], () => { },
            new PlayerPreferences(MileageUnit.Miles, UiLanguage.Dutch));

        Assert.Equal(MileageUnit.Miles, vm.SelectedUnit.Value);
        Assert.Equal(UiLanguage.Dutch, vm.SelectedLanguage.Value);
    }

    [Fact]
    public void Continue_RunsTheDoneAction()
    {
        bool done = false;
        var vm = Create([], () => done = true);

        vm.ContinueCommand.Execute(null);

        Assert.True(done);
    }

    [Fact]
    public void LanguageChange_ReReadsTheCopyProperties()
    {
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            // The production callback applies the UI culture; answer the same
            // way here so the page copy actually flips to Dutch.
            var vm = new PreferencesScreenViewModel(
                PlayerPreferences.Default,
                preferences => CultureInfo.CurrentUICulture =
                    new CultureInfo(preferences.Language == UiLanguage.Dutch ? "nl-NL" : "en-US"),
                () => { });
            var raised = new List<string?>();
            vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

            vm.SelectedLanguage = vm.LanguageOptions[1];

            Assert.Equal("ACEDRIVE INSTELLEN", vm.Title);
            Assert.Contains(nameof(PreferencesScreenViewModel.Title), raised);
            Assert.Contains(nameof(PreferencesScreenViewModel.UnitsLabel), raised);
            Assert.Contains(nameof(PreferencesScreenViewModel.LanguageLabel), raised);
            Assert.Contains(nameof(PreferencesScreenViewModel.ContinueLabel), raised);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }
}
