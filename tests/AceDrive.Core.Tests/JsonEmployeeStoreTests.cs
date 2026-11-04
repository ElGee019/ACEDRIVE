using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>employees.json: missing/corrupt files mean nobody is hired, never a
/// crash; a round trip keeps the crew and the week's roster.</summary>
public sealed class JsonEmployeeStoreTests
{
    [Fact]
    public void MissingFile_AnswersAnEmptyCrew()
    {
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString("N"), "employees.json");
        var log = new TestLog();

        EmployeeState state = new JsonEmployeeStore(file, log).Load();

        Assert.Empty(state.Employees ?? []);
        Assert.Empty(state.Candidates ?? []);
        Assert.Equal(-1, state.RosterWeek);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void RoundTrip_KeepsTheCrewAndTheRoster()
    {
        string file = Path.GetTempFileName();
        try
        {
            var state = new EmployeeState(
                Employees: ["Anna Berg"],
                Candidates: ["Bram de Wit", "Carla Rossi"],
                RosterWeek: 3);
            new JsonEmployeeStore(file, new TestLog()).Save(state);

            EmployeeState loaded = new JsonEmployeeStore(file, new TestLog()).Load();

            Assert.Equal(state.Employees, loaded.Employees);
            Assert.Equal(state.Candidates, loaded.Candidates);
            Assert.Equal(3, loaded.RosterWeek);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void CorruptFile_FallsBackToAnEmptyCrewAndWarns()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();

            EmployeeState state = new JsonEmployeeStore(file, log).Load();

            Assert.Empty(state.Employees ?? []);
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }
}
