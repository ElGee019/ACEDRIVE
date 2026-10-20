using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The appetite mechanic, checked line for line against the Unity source
/// (GameClockManager.UpdateHunger + YouScreen.OnTrunkItemClicked): one gain
/// per crossed game hour, the firstSceneComplete freeze, the cap, the
/// one-shot pancake threshold event and the eating reduce.
/// </summary>
public sealed class HungerTests
{
    private static DateTime Start => new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime PlusHours(int hours) => Start.AddHours(hours);

    // Feed the meter by the exact unit Unity feeds it: one advance per game
    // hour from the given moment, returning the report that landed.
    private static HungerReport AdvanceByHours(
        Hunger hunger, DateTime from, int hours,
        bool onboarded = true, bool hasPancakes = false)
    {
        HungerReport report = new(hunger.Value, ThresholdFired: false, Changed: false);
        for (int i = 0; i < hours; i++)
        {
            report = hunger.AdvanceTo(from.AddHours(i + 1), onboarded, hasPancakes);
        }
        return report;
    }

    [Fact]
    public void FreshMeter_StartsEmpty()
    {
        var hunger = new Hunger(HungerRules.Default);
        Assert.Equal(0, hunger.Value);
        Assert.Equal(36, hunger.Max);
        Assert.False(hunger.Blocks);
    }

    [Fact]
    public void FirstCall_AnchorsWithoutMoving()
    {
        // The meter's anchor: the first game moment it is shown, so a resumed
        // session never pays the hours of earlier ones.
        var hunger = new Hunger(HungerRules.Default);
        HungerReport report = hunger.AdvanceTo(PlusHours(2), onboarded: true, hasPancakes: false);
        Assert.False(report.Changed);
        Assert.Equal(0, report.Hunger);
    }

    [Fact]
    public void Advance_PaysOnePerCrossedGameHour()
    {
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(Start, onboarded: true, hasPancakes: false);

        // 59 game minutes: the hour has not crossed yet.
        Assert.False(hunger.AdvanceTo(Start.AddMinutes(59), true, false).Changed);
        Assert.Equal(0, hunger.Value);
        // The crossing pays exactly once, partial minutes carry over.
        Assert.True(hunger.AdvanceTo(Start.AddHours(1), true, false).Changed);
        Assert.Equal(1, hunger.Value);
        Assert.True(hunger.AdvanceTo(Start.AddHours(2).AddMinutes(30), true, false).Changed);
        Assert.Equal(2, hunger.Value);
    }

    [Fact]
    public void Advance_ClampsAtMaxHunger()
    {
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(Start, onboarded: true, hasPancakes: false);
        _ = AdvanceByHours(hunger, Start, 40);

        Assert.Equal(36, hunger.Value);
        // An hour passing at full hunger is no transition at all (Unity's
        // early return before the clamp) — nothing to log, nothing to show.
        Assert.False(hunger.AdvanceTo(PlusHours(45), true, false).Changed);
        Assert.True(hunger.Blocks);
    }

    [Fact]
    public void Advance_FrozenUntilOnboarding()
    {
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(Start, onboarded: false, hasPancakes: false);
        _ = AdvanceByHours(hunger, Start, 5, onboarded: false);

        Assert.Equal(0, hunger.Value);
        // Waking past the intro starts the appetite from the wake-up, not
        // retroactively over the frozen hours (Unity firstSceneComplete):
        // the anchor reset at the 5th hour, so the next crossing pays once.
        Assert.True(hunger.AdvanceTo(PlusHours(6), true, false).Changed);
        Assert.Equal(1, hunger.Value);
    }

    [Fact]
    public void Threshold_FiresOnceWhenPancakesRideAlong()
    {
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(Start, onboarded: true, hasPancakes: true);

        // The 14th crossed game hour lands the reading exactly on the
        // threshold: the one-shot fires that tick and never again (Unity
        // hungerEventTriggered is never unset).
        _ = AdvanceByHours(hunger, Start, 13, hasPancakes: true);
        Assert.Equal(13, hunger.Value);
        Assert.False(hunger.ThresholdAlarmed);

        HungerReport atThreshold = AdvanceByHours(hunger, PlusHours(13), 1, hasPancakes: true);
        Assert.True(atThreshold.ThresholdFired);
        Assert.True(hunger.ThresholdAlarmed);
        // Later gains never refire it.
        Assert.True(hunger.AdvanceTo(PlusHours(20), true, true).Changed);
        Assert.False(hunger.AdvanceTo(PlusHours(21), true, true).ThresholdFired);
    }

    [Fact]
    public void Threshold_NeedsPancakes()
    {
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(Start, onboarded: true, hasPancakes: false);
        _ = AdvanceByHours(hunger, Start, 14, hasPancakes: false);

        Assert.Equal(14, hunger.Value);
        Assert.False(hunger.ThresholdAlarmed);
    }

    [Fact]
    public void Threshold_JumpingPastNeverFires()
    {
        // Unity checks the event on the exact reading: rules that clear the
        // threshold in one gain tick (PerHour > 1) skip it, ported as-is.
        var rules = new HungerRules(MaxHunger: 40, PerHour: 5, PancakeThreshold: 14);
        var hunger = new Hunger(rules);
        _ = hunger.AdvanceTo(Start, onboarded: true, hasPancakes: true);
        _ = AdvanceByHours(hunger, Start, 15);

        Assert.Equal(40, hunger.Value);
        Assert.False(hunger.ThresholdAlarmed);
    }

    [Fact]
    public void Eat_ReducesAndClampsAtZero()
    {
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(Start, onboarded: true, hasPancakes: false);
        _ = AdvanceByHours(hunger, Start, 20);

        // Grandma's Pancakes: -14 (ItemDatabase), exactly Unity's max(0, ...).
        hunger.Eat(14);
        Assert.Equal(6, hunger.Value);
        hunger.Eat(14);
        Assert.Equal(0, hunger.Value);
        Assert.False(hunger.Blocks);
    }

    [Fact]
    public void Eat_IgnoresNonFoodAmounts()
    {
        var hunger = new Hunger(HungerRules.Default);
        hunger.Eat(14);
        hunger.Eat(-5);
        hunger.Eat(0);
        // Eating never raises hunger: an hour of appetite stays an hour.
        Assert.Equal(0, hunger.Value);
    }

    [Fact]
    public void Blocks_FiresOneHourBeforeFull()
    {
        // Unity MapScreen: hunger >= maxHunger - 1 refuses the job centre.
        var rules = new HungerRules(MaxHunger: 5, PerHour: 1, PancakeThreshold: 2);
        var hunger = new Hunger(rules);
        _ = hunger.AdvanceTo(Start, onboarded: true, hasPancakes: true);

        _ = AdvanceByHours(hunger, Start, 3);
        Assert.Equal(3, hunger.Value);
        Assert.False(hunger.Blocks);
        _ = AdvanceByHours(hunger, PlusHours(3), 1);
        Assert.Equal(4, hunger.Value);
        Assert.True(hunger.Blocks);
        _ = AdvanceByHours(hunger, PlusHours(4), 1);
        Assert.Equal(5, hunger.Value);
        Assert.True(hunger.Blocks);
    }

    [Fact]
    public void Snapshot_RestoresReadingAndOneShotEvent()
    {
        var hunger = new Hunger(HungerRules.Default);
        _ = hunger.AdvanceTo(Start, onboarded: true, hasPancakes: true);
        _ = AdvanceByHours(hunger, Start, 14, hasPancakes: true);
        HungerState saved = hunger.Snapshot();

        var resumed = new Hunger(HungerRules.Default, saved);
        Assert.Equal(14, resumed.Value);
        Assert.True(resumed.ThresholdAlarmed);
        // The alarm survives, later hours change the reading and refire never.
        resumed.AdvanceTo(PlusHours(15), true, hasPancakes: true); // anchor
        Assert.False(resumed.AdvanceTo(PlusHours(16), true, hasPancakes: true).ThresholdFired);
        Assert.Equal(15, resumed.Value);
        Assert.True(resumed.ThresholdAlarmed);
    }

    [Fact]
    public void Snapshot_ClampsOutOfRangeSaves()
    {
        // A cap that shrank between sessions (or a hand-edited save) must
        // not resurrect an impossible reading.
        var resumed = new Hunger(HungerRules.Default, new HungerState(90, false));
        Assert.Equal(36, resumed.Value);
        Assert.True(resumed.Blocks);
    }
}
