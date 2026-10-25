using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// The voice-start log line: which model folder is live, how big it is and the
/// description it ships. That line is what tells the 67 MB mobile model apart
/// from the 128 MB lgraph one once commands start getting misheard, so the
/// degradation paths matter as much as the happy one.
/// </summary>
public sealed class VoskModelInfoTests
{
    [Fact]
    public void Describe_NamesTheFolderItsSizeAndTheShippedReadmeLine()
    {
        string dir = NewTempDir();
        try
        {
            // Model folders keep the acoustic model one level down, so the size
            // has to walk subfolders to be right.
            string acoustic = Path.Combine(dir, "am");
            Directory.CreateDirectory(acoustic);
            File.WriteAllBytes(Path.Combine(acoustic, "final.mdl"), new byte[1024 * 1024]);
            // The real downloads ship a blank line, the description, then
            // licence prose.
            File.WriteAllText(Path.Combine(dir, "README"),
                "\r\nUS English model for mobile Vosk applications\r\n" +
                "Copyright 2020 Alpha Cephei Inc\r\n");

            string text = VoskModelInfo.Describe(dir);

            Assert.Contains(Path.GetFileName(dir), text, StringComparison.Ordinal);
            // One decimal and the unit, whatever the current culture prints
            // for the decimal point.
            Assert.Matches(@"\d+[.,]\d MB", text);
            Assert.Contains("US English model for mobile Vosk applications", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Copyright", text, StringComparison.Ordinal);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Describe_TrailingSeparator_StillNamesTheFolder()
    {
        string dir = NewTempDir();
        try
        {
            string text = VoskModelInfo.Describe(dir + Path.DirectorySeparatorChar);

            Assert.Contains(Path.GetFileName(dir), text, StringComparison.Ordinal);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Describe_ModelWithoutReadme_StillNamesTheSize()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "final.mdl"), new byte[2 * 1024 * 1024]);

            string text = VoskModelInfo.Describe(dir);

            Assert.Contains(Path.GetFileName(dir), text, StringComparison.Ordinal);
            Assert.Matches(@"\d+[.,]\d MB", text);
            Assert.DoesNotContain(" - ", text, StringComparison.Ordinal);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Describe_MissingFolder_SaysSizeUnknownInsteadOfThrowing()
    {
        // A configured path the player never copied the model into is the
        // failure this line has to survive: voice is off, the log still says
        // which model was attempted.
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        string text = VoskModelInfo.Describe(missing);

        Assert.Contains(Path.GetFileName(missing), text, StringComparison.Ordinal);
        Assert.Contains("size unknown", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_LongReadmeLine_IsClippedToOneLogLine()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllText(Path.Combine(dir, "README"), new string('a', 500));

            string text = VoskModelInfo.Describe(dir);

            Assert.EndsWith("...", text, StringComparison.Ordinal);
            Assert.True(text.Length < 200, text);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }
}
