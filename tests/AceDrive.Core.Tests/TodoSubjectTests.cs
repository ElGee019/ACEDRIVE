using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The subject table and its requirement registry (PORTPLAN 46).</summary>
public sealed class TodoSubjectTests
{
    [Fact]
    public void EverySubject_HasARequirement()
    {
        Assert.Equal(14, TodoSubject.All.Count);
        foreach (string subject in TodoSubject.All)
        {
            Assert.True(TodoRequirements.IsRegistered(subject));
            Assert.Equal(subject, TodoRequirements.Get(subject).Subject);
        }
    }

    [Fact]
    public void All_IsUnique()
    {
        Assert.Equal(TodoSubject.All.Count, new HashSet<string>(TodoSubject.All).Count);
    }

    [Fact]
    public void OneTimeSubjects_AreTheVisitsAndTheFollowUp()
    {
        foreach (string subject in new[]
                 {
                     TodoSubject.GoToMaxGroceryStore, TodoSubject.VisitCarWash,
                     TodoSubject.VisitCarDealer, TodoSubject.VisitInsuranceBroker,
                     TodoSubject.VisitBank, TodoSubject.OpenDeliveryCareer,
                 })
        {
            Assert.True(TodoRequirements.Get(subject).IsOneTime);
        }

        Assert.False(TodoRequirements.Get(TodoSubject.TaxiJobs).IsOneTime);
    }

    [Fact]
    public void InitialActivation_WaitsForTheDoorsAndTheGate()
    {
        Assert.False(TodoRequirements.IsInitiallyActive(TodoSubject.VisitCarWash));
        Assert.False(TodoRequirements.IsInitiallyActive(TodoSubject.VisitCarDealer));
        Assert.False(TodoRequirements.IsInitiallyActive(TodoSubject.VisitInsuranceBroker));
        Assert.False(TodoRequirements.IsInitiallyActive(TodoSubject.VisitBank));
        // The follow-up waits for its gate subject, not a door.
        Assert.False(TodoRequirements.IsInitiallyActive(TodoSubject.OpenDeliveryCareer));
        Assert.True(TodoRequirements.IsInitiallyActive(TodoSubject.GoToMaxGroceryStore));
        Assert.True(TodoRequirements.IsInitiallyActive(TodoSubject.TaxiJobs));
    }

    [Fact]
    public void DefaultTable_IsUnitysFourStartingTips()
    {
        TodoTable table = TodoTable.Default;
        Assert.Equal(4, table.Entries.Count);
        Assert.Equal(TodoSubject.CrEarnings, table.Entries[0].SubjectId);
        Assert.Equal(TodoSubject.XpEarnings, table.Entries[1].SubjectId);
        Assert.Equal(TodoSubject.BankStandings, table.Entries[2].SubjectId);
        Assert.Equal(TodoSubject.GoToMaxGroceryStore, table.Entries[3].SubjectId);
        foreach (TodoEntry entry in table.Entries)
        {
            Assert.True(TodoRequirements.IsRegistered(entry.SubjectId));
            Assert.True(entry.Target >= 1);
        }
    }

    [Fact]
    public void IsKnown_RejectsUnknownIds()
    {
        Assert.False(TodoSubject.IsKnown(null));
        Assert.False(TodoSubject.IsKnown(""));
        Assert.False(TodoSubject.IsKnown("CarHaul"));
        Assert.True(TodoSubject.IsKnown(TodoSubject.TaxiJobs));
    }
}
