// "Own {target} cars." (PORTPLAN item 46, Unity OwnCarsEvaluator): the target
// grows 50% per completion, displayed rounded (Unity rounds its float target).
namespace AceDrive.Core;

public sealed class OwnCarsRequirement : ITodoRequirement
{
    public string Subject => TodoSubject.OwnCars;
    public bool IsOneTime => false;
    public double InitialTarget => 2.0;
    public string TipKey => "TodoOwnCars";
    public string? PoiType => null;
    public string? FallbackKey => null;

    public double Current(TodoFacts facts) => facts.OwnedCars;

    public bool IsMet(TodoFacts facts, double target) =>
        facts.OwnedCars >= Math.Floor(target + 0.5);

    public TodoCycle Advance(TodoCycle cycle) => cycle with { Target = cycle.Target * 1.5 };
}
