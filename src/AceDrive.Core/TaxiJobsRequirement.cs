// "Complete {target} taxi jobs." (PORTPLAN item 46, Unity TaxiJobsEvaluator).
// The curve is Unity's exactly: 5, 11, 18, ... (5(p+1) + (p+1)p/2 with the
// already-incremented cycle count).
namespace AceDrive.Core;

public sealed class TaxiJobsRequirement : ITodoRequirement
{
    public string Subject => TodoSubject.TaxiJobs;
    public bool IsOneTime => false;
    public double InitialTarget => 5.0;
    public string TipKey => "TodoTaxiJobs";
    public string? PoiType => null;
    public string? FallbackKey => null;

    public double Current(TodoFacts facts) => facts.TaxiJobs;

    public bool IsMet(TodoFacts facts, double target) => facts.TaxiJobs >= target;

    public TodoCycle Advance(TodoCycle cycle)
    {
        double p = cycle.Progress;
        return cycle with { Target = 5.0 * (p + 1.0) + (p + 1.0) * p / 2.0 };
    }
}
