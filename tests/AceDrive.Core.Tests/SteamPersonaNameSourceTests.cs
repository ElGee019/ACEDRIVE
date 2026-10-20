using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Steam persona lookup: account selection and safe degradation.</summary>
public sealed class SteamPersonaNameSourceTests
{
    private const string TwoUsers = """
        "users"
        {
            "111"
            {
                "AccountName" "old"
                "PersonaName" "Old Name"
                "Timestamp" "100"
            }
            "222"
            {
                "AccountName" "ElGee"
                "PersonaName" "ElGee"
                "Timestamp" "200"
            }
        }
        """;

    [Fact]
    public void AutoLoginUser_SelectsMatchingAccount()
    {
        string file = TempFile(TwoUsers);
        try
        {
            var log = new TestLog();
            var source = new SteamPersonaNameSource(file, "elgee", log);
            Assert.Equal("ElGee", source.TryReadName());
            Assert.Empty(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void UnknownAutoLoginUser_SelectsMostRecent()
    {
        string file = TempFile(TwoUsers);
        try
        {
            var source = new SteamPersonaNameSource(file, "missing", new TestLog());
            Assert.Equal("ElGee", source.TryReadName());
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void NoAutoLoginUser_SelectsMostRecent()
    {
        string file = TempFile(TwoUsers);
        try
        {
            var source = new SteamPersonaNameSource(file, null, new TestLog());
            Assert.Equal("ElGee", source.TryReadName());
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void MissingFile_ReturnsNullWithWarning()
    {
        var log = new TestLog();
        var source = new SteamPersonaNameSource(TempFile(string.Empty), "elgee", log);
        Assert.Null(source.TryReadName());
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void MalformedFile_ReturnsNullWithWarning()
    {
        string file = TempFile("""
            "users"
            {
                "222"
                {
                    "AccountName" "ElGee"
            """);
        try
        {
            var log = new TestLog();
            var source = new SteamPersonaNameSource(file, "elgee", log);
            Assert.Null(source.TryReadName());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void MissingPersona_ReturnsNullWithWarning()
    {
        string file = TempFile("""
            "users"
            {
                "222"
                {
                    "AccountName" "ElGee"
                    "Timestamp" "200"
                }
            }
            """);
        try
        {
            var log = new TestLog();
            var source = new SteamPersonaNameSource(file, "elgee", log);
            Assert.Null(source.TryReadName());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void CommentsAndEscapedQuotes_AreHandled()
    {
        string file = TempFile("""
            // Valve KeyValues comments are legal.
            "users"
            {
                "222"
                {
                    "AccountName" "ElGee"
                    "PersonaName" "El \"Gee\""
                    "Timestamp" "200" // unix seconds
                }
            }
            """);
        try
        {
            var source = new SteamPersonaNameSource(file, "elgee", new TestLog());
            Assert.Equal("El \"Gee\"", source.TryReadName());
        }
        finally { File.Delete(file); }
    }

    private static string TempFile(string content)
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".vdf");
        File.WriteAllText(file, content);
        // The missing-file test needs a path that does not exist.
        if (content.Length == 0)
        {
            File.Delete(file);
        }
        return file;
    }
}
