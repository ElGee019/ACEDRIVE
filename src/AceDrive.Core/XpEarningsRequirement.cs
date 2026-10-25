// "Earn {target} XP in total." (PORTPLAN item 46, Unity XpEarningsEvaluator):
// the target doubles per completion.
namespace AceDrive.Core;

public sealed class XpEarningsRequirement : ITodoRequirement
{
    public string Subject => TodoSubject.XpEarnings;
    public bool IsOneTime => false;
    public double InitialTarget => 2.0;
    public string TipKey => "TodoEarnXp";
    public string? PoiType => null;
    public string? FallbackKey => null;

    public double Current(TodoFacts facts) => facts.Xp;

    public bool IsMet(TodoFacts facts, double target) => facts.Xp >= target;

    public TodoCycle Advance(TodoCycle cycle) => cycle with { Target = cycle.Target * 2.0 };
}
