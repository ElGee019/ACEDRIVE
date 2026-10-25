using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>The log sink: a dated file under the configured folder, plus a
/// one-time move to the fallback folder when that sink cannot be written.
/// A mute log is what made a failed game reset undiagnosable, so the fallback
/// is behaviour worth pinning.</summary>
public sealed class FileLoggerTests
{
    [Fact]
    public void LogInfo_WritesTheDatedFileAndLeavesTheFallbackAlone()
    {
        using var root = new TempRoot();
        string dir = Path.Combine(root.Path, "logs");
        string fallback = Path.Combine(root.Path, "fallback");

        using (var log = new FileLogger(dir, fallback))
        {
            log.LogInfo("hello");
        }

        string file = Assert.Single(Directory.GetFiles(dir));
        Assert.Contains("INFO  hello", File.ReadAllText(file));
        Assert.False(Directory.Exists(fallback));
    }

    [Fact]
    public void LogInfo_UnwritableSink_MovesToTheFallbackAndKeepsWriting()
    {
        using var root = new TempRoot();
        // A file where the folder should be: CreateDirectory fails, exactly like
        // a locked or read-only log directory.
        string broken = Path.Combine(root.Path, "logs");
        File.WriteAllText(broken, "not a folder");
        string fallback = Path.Combine(root.Path, "fallback");

        using (var log = new FileLogger(broken, fallback))
        {
            log.LogInfo("hello");
            log.LogWarning("again");
        }

        string file = Assert.Single(Directory.GetFiles(fallback));
        string text = File.ReadAllText(file);
        Assert.Contains("INFO  hello", text);
        Assert.Contains("WARN  again", text);
        // The reader has to learn why the usual file is empty.
        Assert.Contains("unavailable", text);
    }

    [Fact]
    public void LogInfo_BothSinksUnavailable_WritesNothingAndDoesNotThrow()
    {
        using var root = new TempRoot();
        string broken = Path.Combine(root.Path, "logs");
        File.WriteAllText(broken, "not a folder");
        string brokenFallback = Path.Combine(root.Path, "fallback");
        File.WriteAllText(brokenFallback, "not a folder either");

        using var log = new FileLogger(broken, brokenFallback);

        // Logging must never break the app, whatever the file system says.
        log.LogInfo("hello");
        log.LogError("again");

        Assert.Equal("not a folder either", File.ReadAllText(brokenFallback));
    }

    private sealed class TempRoot : IDisposable
    {
        public TempRoot()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
