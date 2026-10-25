using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The one-time business visits the ToDo list asks for (PORTPLAN 46).</summary>
public sealed class VisitLedgerTests
{
    [Fact]
    public void Fresh_StartsUnvisited()
    {
        var visits = new VisitLedger();
        Assert.False(visits.CarWash);
        Assert.False(visits.CarDealer);
        Assert.False(visits.InsuranceBroker);
    }

    [Fact]
    public void Marks_AreIndependentAndResumeFromTheDriver()
    {
        var visits = new VisitLedger(new DriverState(
            0.0, 0.0, VisitedCarWash: true, VisitedInsurance: true));

        Assert.True(visits.CarWash);
        Assert.False(visits.CarDealer);
        Assert.True(visits.InsuranceBroker);

        visits.MarkCarDealer();
        Assert.True(visits.CarDealer);
        Assert.True(visits.CarWash); // the other flags never reset
    }
}
