using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>preferences.json: missing/corrupt files mean the defaults, never a crash.</summary>
public sealed class JsonPreferencesStoreTests
{
    [Fact]
    public void MissingFile_AnswersDefaults()
    {
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString("N"), "preferences.json");
        var log = new TestLog();

        Assert.Equal(PlayerPreferences.Default, new JsonPreferencesStore(file, log).Load());
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void RoundTrip_KeepsBothChoices()
    {
        string file = Path.GetTempFileName();
        try
        {
            var preferences = new PlayerPreferences(MileageUnit.Miles, UiLanguage.Dutch);
            new JsonPreferencesStore(file, new TestLog()).Save(preferences);

            Assert.Equal(preferences, new JsonPreferencesStore(file, new TestLog()).Load());
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void CorruptFile_FallsBackToDefaultsAndWarns()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();

            Assert.Equal(PlayerPreferences.Default, new JsonPreferencesStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void UnknownUnit_FallsBackToDefaultsAndWarns()
    {
        string file = Path.GetTempFileName();
        try
        {
            // A hand-edited file can name a value the enum no longer has; the
            // page must not select an undefined option.
            File.WriteAllText(file, """{"Units":99,"Language":0}""");
            var log = new TestLog();

            Assert.Equal(PlayerPreferences.Default, new JsonPreferencesStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void RoundTrip_KeepsTheAssignedCall()
    {
        string file = Path.GetTempFileName();
        try
        {
            var call = new CallBinding(
                CallBindingKind.HidButton, 3, CallModifiers.None, "Button 3", "VID_046D&PID_C24F");
            var preferences =
                new PlayerPreferences(MileageUnit.Miles, UiLanguage.Dutch) { Call = call };
            new JsonPreferencesStore(file, new TestLog()).Save(preferences);

            PlayerPreferences loaded = new JsonPreferencesStore(file, new TestLog()).Load();

            Assert.Equal(preferences, loaded);
            Assert.Equal(call, loaded.Call);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void UnusableCall_FallsBackToTheDefaultChordAndKeepsTheRest()
    {
        // A save that names an input the listener cannot arm would leave the
        // panel unreachable by input, so only that field falls back.
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(
                file,
                """{"Units":1,"Language":1,"Call":{"Kind":9,"Code":0,"Modifiers":0,"Label":""}}""");
            var log = new TestLog();

            PlayerPreferences loaded = new JsonPreferencesStore(file, log).Load();

            Assert.Null(loaded.Call);
            Assert.Equal(MileageUnit.Miles, loaded.Units);
            Assert.Equal(UiLanguage.Dutch, loaded.Language);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
