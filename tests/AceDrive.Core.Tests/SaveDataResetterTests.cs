using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>RESET GAME's file wipe: career files go, settings and identity stay.</summary>
public sealed class SaveDataResetterTests
{
    [Fact]
    public void DeleteAll_RemovesCareerFiles_AndKeepsSettings()
    {
        using var dir = new TempDirectory();
        string[] career =
        [
            "onboarding.json", "profile.json", "driver.json", "garage.json",
            "city.json", "hunger.json", "gameclock.json", "roadtest.json",
            "shift.json", "friendship.json", "brand_mileage.json",
            "residents.json", "addresses.json",
        ];
        foreach (string name in career)
        {
            File.WriteAllText(Path.Combine(dir.Path, name), "{}");
        }
        string[] kept = ["preferences.json", "window.json", "hud.json", "firebase.json"];
        foreach (string name in kept)
        {
            File.WriteAllText(Path.Combine(dir.Path, name), "{}");
        }

        var log = new TestLog();
        bool complete = new FileSaveDataResetter(dir.Path, log).DeleteAll();

        Assert.True(complete);
        Assert.All(career, name =>
            Assert.False(File.Exists(Path.Combine(dir.Path, name)), name));
        Assert.All(kept, name =>
            Assert.True(File.Exists(Path.Combine(dir.Path, name)), name));
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void DeleteAll_MissingDirectory_ReportsComplete()
    {
        using var dir = new TempDirectory();
        var log = new TestLog();

        bool complete = new FileSaveDataResetter(dir.Path, log).DeleteAll();

        Assert.True(complete);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void DeleteAll_MissingFiles_ReportsComplete()
    {
        using var dir = new TempDirectory();
        var log = new TestLog();

        // Only a settings file exists: a fresh install has almost nothing to
        // wipe, and a no-op reset must still count as complete.
        File.WriteAllText(Path.Combine(dir.Path, "preferences.json"), "{}");

        Assert.True(new FileSaveDataResetter(dir.Path, log).DeleteAll());
        Assert.True(File.Exists(Path.Combine(dir.Path, "preferences.json")));
        Assert.Empty(log.Warnings);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
