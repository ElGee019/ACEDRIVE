using System.Globalization;

namespace AceDrive.Core.Tests;

/// <summary>
/// The employees page's on-job state: assign, query and clear, keyed by hire
/// name case-insensitively (the same comparison the roster uses), and every
/// change banked so the next session resumes the same crew at work.
/// </summary>
public sealed class EmployeeAssignmentsTests
{
    public EmployeeAssignmentsTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    [Fact]
    public void Assign_MarksTheEmployeeOnJobWithThatFamily()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());

        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing);

        Assert.True(assignments.IsOnJob("Anna Berg"));
        Assert.Equal(EmployeeJobKind.CarSharing, assignments.JobOf("Anna Berg"));
    }

    [Fact]
    public void Names_MatchCaseInsensitively()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        assignments.Assign("anna berg", EmployeeJobKind.TaxiJobs);

        Assert.True(assignments.IsOnJob("Anna Berg"));
        Assert.Equal(EmployeeJobKind.TaxiJobs, assignments.JobOf("ANNA BERG"));
    }

    [Fact]
    public void Clear_TakesTheEmployeeOffTheirJob()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        assignments.Assign("Bram de Wit", EmployeeJobKind.DeliveryJobs);

        assignments.Clear("Bram de Wit");

        Assert.False(assignments.IsOnJob("Bram de Wit"));
        Assert.Null(assignments.JobOf("Bram de Wit"));
    }

    [Fact]
    public void BlankName_IsNeverOnJob()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());

        assignments.Assign("  ", EmployeeJobKind.CarSharing);

        Assert.False(assignments.IsOnJob("  "));
        Assert.Null(assignments.JobOf(""));
    }

    [Fact]
    public void UnknownEmployee_IsNotOnJob()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());

        Assert.False(assignments.IsOnJob("Nobody"));
        Assert.Null(assignments.JobOf("Nobody"));
    }

    [Fact]
    public void TimedAssignment_IsDueOnlyOnceTheGameMinuteArrives()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, 500, 0.34);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.AssignmentOf("Anna Berg"));
        Assert.False(job.IsDue(499));
        Assert.True(job.IsDue(500));
        Assert.Equal(0.34, job.Xp, precision: 10);
    }

    [Fact]
    public void TakeFinished_OnlyTakesAJobWhoseMinutePassed()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, 500, 0.34);

        Assert.Null(assignments.TakeFinished("Anna Berg", 499));
        Assert.True(assignments.IsOnJob("Anna Berg"));

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            assignments.TakeFinished("Anna Berg", 500));
        Assert.Equal(0.34, job.Xp, precision: 10);
        Assert.False(assignments.IsOnJob("Anna Berg"));
        Assert.Null(assignments.TakeFinished("Anna Berg", 501));
    }

    [Fact]
    public void UntimedAssignment_IsNeverFinished()
    {
        var assignments = new EmployeeAssignments(new FakeEmployeeJobStore());
        assignments.Assign("Bram de Wit", EmployeeJobKind.TaxiJobs);

        Assert.Null(assignments.TakeFinished("Bram de Wit", long.MaxValue));
        Assert.True(assignments.IsOnJob("Bram de Wit"));
    }

    [Fact]
    public void Assign_IsBankedSoTheNextSessionResumesTheJob()
    {
        var store = new FakeEmployeeJobStore();
        new EmployeeAssignments(store)
            .Assign("Anna Berg", EmployeeJobKind.CarSharing, 500, 0.34);

        var resumed = new EmployeeAssignments(store);

        EmployeeJobAssignment job = Assert.IsType<EmployeeJobAssignment>(
            resumed.AssignmentOf("Anna Berg"));
        Assert.Equal(EmployeeJobKind.CarSharing, job.Kind);
        Assert.Equal(500, job.DueGameMinute);
        Assert.Equal(0.34, job.Xp, precision: 10);
    }

    [Fact]
    public void AssignOnAResumedInstance_KeepsTheJobsItHasNotReadYet()
    {
        var store = new FakeEmployeeJobStore
        {
            State = new EmployeeJobState(
                new Dictionary<string, EmployeeJobAssignment>
                {
                    ["Anna Berg"] = new(EmployeeJobKind.CarSharing, 500, 0.34),
                }),
        };

        var assignments = new EmployeeAssignments(store);
        assignments.Assign("Bram de Wit", EmployeeJobKind.TaxiJobs);

        var resumed = new EmployeeAssignments(store);
        Assert.True(resumed.IsOnJob("Anna Berg"));
        Assert.True(resumed.IsOnJob("Bram de Wit"));
    }

    [Fact]
    public void Clear_IsBankedSoTheNextSessionIsIdle()
    {
        var store = new FakeEmployeeJobStore();
        var assignments = new EmployeeAssignments(store);
        assignments.Assign("Bram de Wit", EmployeeJobKind.DeliveryJobs);

        assignments.Clear("Bram de Wit");

        Assert.False(new EmployeeAssignments(store).IsOnJob("Bram de Wit"));
    }

    [Fact]
    public void TakeFinished_IsBankedSoTheJobCannotPayTwice()
    {
        var store = new FakeEmployeeJobStore();
        var assignments = new EmployeeAssignments(store);
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing, 500, 0.34);

        Assert.NotNull(assignments.TakeFinished("Anna Berg", 500));

        var resumed = new EmployeeAssignments(store);
        Assert.False(resumed.IsOnJob("Anna Berg"));
        Assert.Null(resumed.TakeFinished("Anna Berg", 500));
    }

    [Fact]
    public void TheStoreIsReadOncePerSession()
    {
        var store = new FakeEmployeeJobStore();
        var assignments = new EmployeeAssignments(store);
        assignments.Assign("Anna Berg", EmployeeJobKind.CarSharing);

        assignments.IsOnJob("Anna Berg");
        assignments.JobOf("Bram de Wit");

        Assert.Equal(1, store.Loads);
    }

    [Fact]
    public void BlankName_WritesNothing()
    {
        var store = new FakeEmployeeJobStore();
        var assignments = new EmployeeAssignments(store);

        assignments.Assign("  ", EmployeeJobKind.CarSharing);
        assignments.Clear("");

        Assert.Empty(store.Saves);
        Assert.Equal(0, store.Loads);
    }
}
