using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>RESET GAME's file wipe: career files go, settings and identity stay,
/// and the report names the file that survived so the page can point at it
/// instead of telling the player to close the overlay that just refused.</summary>
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
            "residents.json", "addresses.json", "todos_state.json",
        ];
        foreach (string name in career)
        {
            File.WriteAllText(Path.Combine(dir.Path, name), "{}");
            // The JSON boundary writes both companions; a surviving backup
            // would resurrect the career on the next boot.
            File.WriteAllText(Path.Combine(dir.Path, name + ".bak"), "{}");
            File.WriteAllText(Path.Combine(dir.Path, name + ".tmp"), "{}");
        }
        string[] kept = ["preferences.json", "window.json", "hud.json", "firebase.json"];
        foreach (string name in kept)
        {
            File.WriteAllText(Path.Combine(dir.Path, name), "{}");
        }

        var log = new TestLog();
        SaveResetReport report = new FileSaveDataResetter(dir.Path, log).DeleteAll();

        Assert.True(report.Complete);
        Assert.Null(report.BlockedFile);
        Assert.Equal(SaveResetBlock.None, report.Block);
        Assert.All(career, name =>
            Assert.False(File.Exists(Path.Combine(dir.Path, name)), name));
        Assert.All(career, name =>
            Assert.False(File.Exists(Path.Combine(dir.Path, name + ".bak")), name + ".bak"));
        Assert.All(career, name =>
            Assert.False(File.Exists(Path.Combine(dir.Path, name + ".tmp")), name + ".tmp"));
        Assert.All(kept, name =>
            Assert.True(File.Exists(Path.Combine(dir.Path, name)), name));
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void DeleteAll_BackupHeldOpen_LeavesThatSaveIntact()
    {
        using var dir = new TempDirectory();
        string primary = Path.Combine(dir.Path, "driver.json");
        string backup = primary + ".bak";
        File.WriteAllText(primary, "{}");
        File.WriteAllText(backup, "{}");
        var log = new TestLog();

        SaveResetReport report;
        using (new FileStream(backup, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            report = new FileSaveDataResetter(dir.Path, log).DeleteAll();
        }

        // The backup recovers the career, so a wipe that cannot remove it must
        // not remove the primary either: a half wipe would come back on boot.
        Assert.False(report.Complete);
        Assert.Equal("driver.json.bak", report.BlockedFile);
        Assert.Equal(SaveResetBlock.InUse, report.Block);
        Assert.True(File.Exists(primary));
        Assert.NotEmpty(log.Warnings);
    }

    [Fact]
    public void DeleteAll_LockedTemp_StillRemovesTheSave()
    {
        using var dir = new TempDirectory();
        string primary = Path.Combine(dir.Path, "driver.json");
        string temp = primary + ".tmp";
        File.WriteAllText(primary, "{}");
        File.WriteAllText(temp, "{}");
        var log = new TestLog();

        SaveResetReport report;
        using (new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            report = new FileSaveDataResetter(dir.Path, log).DeleteAll();
        }

        // A temp file is never read: leaving it behind cannot resurrect the
        // career, so it must not turn a complete wipe into a blocked one.
        Assert.True(report.Complete);
        Assert.False(File.Exists(primary));
        Assert.Contains(log.Warnings, w => w.Contains("driver.json.tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void DeleteAll_MissingDirectory_ReportsComplete()
    {
        using var dir = new TempDirectory();
        var log = new TestLog();

        SaveResetReport report = new FileSaveDataResetter(dir.Path, log).DeleteAll();

        Assert.True(report.Complete);
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

        Assert.True(new FileSaveDataResetter(dir.Path, log).DeleteAll().Complete);
        Assert.True(File.Exists(Path.Combine(dir.Path, "preferences.json")));
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void DeleteAll_FileHeldOpen_NamesItAsInUse()
    {
        using var dir = new TempDirectory();
        string blocked = Path.Combine(dir.Path, "driver.json");
        File.WriteAllText(blocked, "{}");
        File.WriteAllText(Path.Combine(dir.Path, "garage.json"), "{}");
        var log = new TestLog();

        SaveResetReport report;
        using (new FileStream(blocked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            report = new FileSaveDataResetter(dir.Path, log).DeleteAll();
        }

        Assert.False(report.Complete);
        Assert.Equal("driver.json", report.BlockedFile);
        Assert.Equal(SaveResetBlock.InUse, report.Block);
        // The rest of the wipe still ran: one stuck file costs one record, and
        // the report points at the file the player has to unblock.
        Assert.False(File.Exists(Path.Combine(dir.Path, "garage.json")));
        Assert.True(File.Exists(blocked));
        Assert.NotEmpty(log.Warnings);
    }

    [Fact]
    public void DeleteAll_ReadOnlyFile_NamesItAsDenied()
    {
        using var dir = new TempDirectory();
        string blocked = Path.Combine(dir.Path, "driver.json");
        File.WriteAllText(blocked, "{}");
        File.SetAttributes(blocked, FileAttributes.ReadOnly);
        try
        {
            SaveResetReport report =
                new FileSaveDataResetter(dir.Path, new TestLog()).DeleteAll();

            Assert.False(report.Complete);
            Assert.Equal("driver.json", report.BlockedFile);
            Assert.Equal(SaveResetBlock.Denied, report.Block);
        }
        finally
        {
            // The temp directory cannot be removed with a read-only file in it.
            File.SetAttributes(blocked, FileAttributes.Normal);
        }
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
