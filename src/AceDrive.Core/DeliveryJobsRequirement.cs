// "Complete {target} delivery jobs." (PORTPLAN item 46, Unity
// DeliveryJobsEvaluator). Unity's curve: 2, 10, 18, ... (10p + p(p-1)).
namespace AceDrive.Core;

public sealed class DeliveryJobsRequirement : ITodoRequirement
{
    public string Subject => TodoSubject.DeliveryJobsCompleted;
    public bool IsOneTime => false;
    public double InitialTarget => 2.0;
    public string TipKey => "TodoDeliveryJobs";
    public string? PoiType => null;
    public string? FallbackKey => null;

    public double Current(TodoFacts facts) => facts.DeliveryJobs;

    public bool IsMet(TodoFacts facts, double target) => facts.DeliveryJobs >= target;

    public TodoCycle Advance(TodoCycle cycle)
    {
        double p = cycle.Progress;
        return cycle with { Target = 10.0 * p + p * (p - 1.0) };
    }
}
