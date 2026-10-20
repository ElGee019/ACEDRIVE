using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The trip-start breakdown roll (PORTPLAN item 39, port of Unity
/// StandardBreakdownStrategy): a zero chance never fires, a guaranteed chance
/// always does, and the hit lands in the 10%..90% route window.</summary>
public sealed class BreakdownStrategyTests
{
    [Fact]
    public void ZeroChance_NeverTriggers()
    {
        var strategy = new BreakdownStrategy(MaintenanceRules.Default, seed: 0);
        for (int i = 0; i < 1000; i++)
        {
            Assert.Null(strategy.TryTrigger(0.0));
        }
    }

    [Fact]
    public void GuaranteedChance_AlwaysTriggersInsideTheWindow()
    {
        var strategy = new BreakdownStrategy(MaintenanceRules.Default, seed: 7);
        for (int i = 0; i < 100; i++)
        {
            double? fraction = strategy.TryTrigger(1.0);
            Assert.NotNull(fraction);
            Assert.InRange(fraction!.Value, 0.10, 0.90);
        }
    }

    [Fact]
    public void SameSeed_ReproducesTheSameRolls()
    {
        var a = new BreakdownStrategy(MaintenanceRules.Default, seed: 42);
        var b = new BreakdownStrategy(MaintenanceRules.Default, seed: 42);
        for (int i = 0; i < 50; i++)
        {
            Assert.Equal(a.TryTrigger(0.5), b.TryTrigger(0.5));
        }
    }
}
