using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Data-file degradation: missing/invalid todos.json falls back
/// to built-in defaults with a warning â€” never an exception.</summary>
public sealed class JsonTodoStoreTests
{
    [Fact]
    public void MissingFile_FallsBackWithWarning()
    {
        var log = new TestLog();
        var store = new JsonTodoStore(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "todos.json"), log);
        Assert.Same(TodoTable.Default, store.Load());
        Assert.Single(log.Warnings);
    }

    [Fact]
    public void InvalidContent_FallsBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();
            Assert.Same(TodoTable.Default, new JsonTodoStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void UnknownSubject_FallsBackWithWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """{"Entries":[{"SubjectId":"CarHaul","Target":1}]}""");
            var log = new TestLog();
            Assert.Same(TodoTable.Default, new JsonTodoStore(file, log).Load());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ValidFile_LoadsWithoutWarning()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, """{"Entries":[{"SubjectId":"TaxiJobs","Target":5}]}""");
            var log = new TestLog();
            TodoTable table = new JsonTodoStore(file, log).Load();
            Assert.Empty(log.Warnings);
            var entry = Assert.Single(table.Entries);
            Assert.Equal("TaxiJobs", entry.SubjectId);
            Assert.Equal(5, entry.Target);
        }
        finally { File.Delete(file); }
    }
}
