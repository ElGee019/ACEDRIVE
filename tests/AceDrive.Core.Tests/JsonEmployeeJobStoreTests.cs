using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>
/// employee_jobs.json: missing/corrupt files mean nobody is on a job, never a
/// crash; a round trip keeps each employee's family, due minute and XP, and the
/// page over a reopened store resumes the same rows.
/// </summary>
public sealed class JsonEmployeeJobStoreTests
{
    [Fact]
    public void MissingFile_AnswersNobodyOnAJob()
    {
        string file = Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString("N"), "employee_jobs.json");
        var log = new TestLog();

        EmployeeJobState state = new JsonEmployeeJobStore(file, log).Load();

        Assert.Empty(state.Jobs ?? new Dictionary<string, EmployeeJobAssignment>());
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void RoundTrip_KeepsTheTimedJobAndTheUntimedOne()
    {
        string file = Path.GetTempFileName();
        try
        {
            var state = new EmployeeJobState(
                new Dictionary<string, EmployeeJobAssignment>
                {
                    ["Anna Berg"] = new(EmployeeJobKind.CarSharing, 500, 0.34),
                    ["Bram de Wit"] = new(EmployeeJobKind.TaxiJobs),
                });
            new JsonEmployeeJobStore(file, new TestLog()).Save(state);

            EmployeeJobState loaded = new JsonEmployeeJobStore(file, new TestLog()).Load();

            // WHY the raw text: the family is written as a name on purpose, so
            // a reordered enum can never turn one family into another.
            Assert.Contains("CarSharing", File.ReadAllText(file), StringComparison.Ordinal);
            Assert.NotNull(loaded.Jobs);
            EmployeeJobAssignment timed = loaded.Jobs["Anna Berg"];
            Assert.Equal(EmployeeJobKind.CarSharing, timed.Kind);
            Assert.Equal(500, timed.DueGameMinute);
            Assert.Equal(0.34, timed.Xp, precision: 10);
            EmployeeJobAssignment untimed = loaded.Jobs["Bram de Wit"];
            Assert.Equal(EmployeeJobKind.TaxiJobs, untimed.Kind);
            Assert.Null(untimed.DueGameMinute);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void CorruptFile_FallsBackToNobodyAndWarns()
    {
        string file = Path.GetTempFileName();
        try
        {
            File.WriteAllText(file, "{ not json");
            var log = new TestLog();

            EmployeeJobState state = new JsonEmployeeJobStore(file, log).Load();

            Assert.Empty(state.Jobs ?? new Dictionary<string, EmployeeJobAssignment>());
            Assert.Single(log.Warnings);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void ARestart_OverTheSameFile_ResumesTheRunningJob()
    {
        string file = Path.GetTempFileName();
        try
        {
            new EmployeeAssignments(new JsonEmployeeJobStore(file, new TestLog()))
                .Assign("Anna Berg", EmployeeJobKind.CarSharing, 500, 0.34);

            var resumed = new EmployeeAssignments(new JsonEmployeeJobStore(file, new TestLog()));

            EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
                resumed.AssignmentOf("Anna Berg"));
            Assert.Equal(EmployeeJobKind.CarSharing, job.Kind);
            Assert.False(job.IsDue(499));
            Assert.True(job.IsDue(500));
        }
        finally { File.Delete(file); }
    }
}
