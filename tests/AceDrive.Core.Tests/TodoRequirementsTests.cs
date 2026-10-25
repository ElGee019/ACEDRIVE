using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The requirement curves, pinned to Unity's TodoEvaluators values
/// (PORTPLAN 46): a threshold that drifts would silently change the care-
/// package pace.
/// </summary>
public sealed class TodoRequirementsTests
{
    [Fact]
    public void CrEarnings_ClimbsFiftyPercent()
    {
        var requirement = new CrEarningsRequirement();
        Assert.Equal(500.0, requirement.InitialTarget);
        Assert.Equal(750.0, requirement.Advance(new TodoCycle(500.0, 1, 0)).Target);
        Assert.Equal(1125.0, requirement.Advance(new TodoCycle(750.0, 2, 0)).Target);
    }

    [Fact]
    public void XpEarnings_Doubles()
    {
        var requirement = new XpEarningsRequirement();
        Assert.Equal(2.0, requirement.InitialTarget);
        Assert.Equal(4.0, requirement.Advance(new TodoCycle(2.0, 1, 0)).Target);
    }

    [Fact]
    public void BankStandings_RidesTheInterestAllowance()
    {
        var requirement = new BankStandingsRequirement();
        Assert.Equal(1002.0, requirement.InitialTarget);
        TodoCycle next = requirement.Advance(new TodoCycle(1002.0, 1, 2));
        Assert.Equal(4, next.BankInterest);
        Assert.Equal(1004.0, next.Target);
        next = requirement.Advance(next with { Progress = 2 });
        Assert.Equal(1008.0, next.Target);

        // Unity floors the allowance at 1 before doubling, so a broken save
        // with 0 moves to 2 rather than staying stuck.
        Assert.Equal(2, requirement.Advance(new TodoCycle(1000.0, 1, 0)).BankInterest);
    }

    [Fact]
    public void TaxiJobs_FollowsUnitysCurve()
    {
        var requirement = new TaxiJobsRequirement();
        Assert.Equal(5.0, requirement.InitialTarget);
        Assert.Equal(11.0, requirement.Advance(new TodoCycle(5.0, 1, 0)).Target);
        Assert.Equal(18.0, requirement.Advance(new TodoCycle(11.0, 2, 0)).Target);
    }

    [Fact]
    public void DeliveryJobs_FollowsUnitysCurve()
    {
        var requirement = new DeliveryJobsRequirement();
        Assert.Equal(2.0, requirement.InitialTarget);
        Assert.Equal(10.0, requirement.Advance(new TodoCycle(2.0, 1, 0)).Target);
        Assert.Equal(22.0, requirement.Advance(new TodoCycle(10.0, 2, 0)).Target);
    }

    [Fact]
    public void SetDestinations_StepsEighteen()
    {
        var requirement = new SetDestinationsRequirement();
        Assert.Equal(18.0, requirement.InitialTarget);
        Assert.Equal(36.0, requirement.Advance(new TodoCycle(18.0, 1, 0)).Target);
    }

    [Fact]
    public void OperatingZone_ClimbsFiftyPercent()
    {
        var requirement = new OperatingZoneRequirement();
        Assert.Equal(4.0, requirement.InitialTarget);
        Assert.Equal(6.0, requirement.Advance(new TodoCycle(4.0, 1, 0)).Target);
    }

    [Fact]
    public void OwnCars_ClimbsFiftyPercentAndRoundsForDisplay()
    {
        var requirement = new OwnCarsRequirement();
        Assert.Equal(2.0, requirement.InitialTarget);
        Assert.Equal(3.0, requirement.Advance(new TodoCycle(2.0, 1, 0)).Target);
        Assert.Equal(4.5, requirement.Advance(new TodoCycle(3.0, 2, 0)).Target);

        // Unity displays floor(target + 0.5): 4.5 cars reads as "Own 5 cars".
        var facts = new TodoFacts(0, 0, 0, 0, 0, 0, 0, 4, false, false, false, false, false, false);
        Assert.False(requirement.IsMet(facts, 4.5));
        Assert.True(requirement.IsMet(facts with { OwnedCars = 5 }, 4.5));
    }

    [Fact]
    public void VisitRequirement_IsOneTimeAndReadsItsFlag()
    {
        var requirement = new VisitTodoRequirement(
            TodoSubject.VisitCarWash, "TodoEnterPoi", "TodoVisitCarWash",
            facts => facts.CarWashEntered, PoiTypes.CarWash);

        Assert.True(requirement.IsOneTime);
        Assert.Equal(PoiTypes.CarWash, requirement.PoiType);
        var facts = new TodoFacts(0, 0, 0, 0, 0, 0, 0, 0, false, false, false, false, false, false);
        Assert.False(requirement.IsMet(facts, 0));
        Assert.True(requirement.IsMet(facts with { CarWashEntered = true }, 0));
        Assert.Equal(1.0, requirement.Current(facts with { CarWashEntered = true }));
    }

    [Fact]
    public void FollowUp_IsOneTimeAndNamesItsGate()
    {
        var requirement = new FollowUpTodoRequirement(
            TodoSubject.OpenDeliveryCareer, TodoSubject.GoToMaxGroceryStore,
            "TodoOpenDeliveryCareer", facts => facts.DeliveryCareerOpened);

        Assert.True(requirement.IsOneTime);
        Assert.Equal(TodoSubject.GoToMaxGroceryStore, requirement.UnlockedBy);
        Assert.Null(requirement.PoiType);
        var facts = new TodoFacts(0, 0, 0, 0, 0, 0, 0, 0, false, false, false, false, false, false);
        Assert.False(requirement.IsMet(facts, 0));
        Assert.True(requirement.IsMet(facts with { DeliveryCareerOpened = true }, 0));
        Assert.Equal(1.0, requirement.Current(facts with { DeliveryCareerOpened = true }));
    }
}
