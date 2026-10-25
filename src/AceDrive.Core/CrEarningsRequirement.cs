// "Earn {target} CR in total." (PORTPLAN item 46, Unity CrEarningsEvaluator):
// the lifetime total climbs 50% per completion.
namespace AceDrive.Core;

public sealed class CrEarningsRequirement : ITodoRequirement
{
    public string Subject => TodoSubject.CrEarnings;
    public bool IsOneTime => false;
    public double InitialTarget => 500.0;
    public string TipKey => "TodoEarnCr";
    public string? PoiType => null;
    public string? FallbackKey => null;

    public double Current(TodoFacts facts) => facts.CreditEarned;

    public bool IsMet(TodoFacts facts, double target) => facts.CreditEarned >= target;

    // Unity: current + current / 2 (integer division on an int target).
    public TodoCycle Advance(TodoCycle cycle) =>
        cycle with { Target = cycle.Target + Math.Floor(cycle.Target / 2.0) };
}
