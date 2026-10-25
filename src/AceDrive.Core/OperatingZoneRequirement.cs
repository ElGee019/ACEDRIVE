// "Reach an operating zone of {target} km." (PORTPLAN item 46, Unity
// OperatingZoneEvaluator): the zone's discovered km, target +50% per cycle.
namespace AceDrive.Core;

public sealed class OperatingZoneRequirement : ITodoRequirement
{
    public string Subject => TodoSubject.OperatingZone;
    public bool IsOneTime => false;
    public double InitialTarget => 4.0;
    public string TipKey => "TodoOperatingZone";
    public string? PoiType => null;
    public string? FallbackKey => null;

    public double Current(TodoFacts facts) => facts.ZoneKm;

    public bool IsMet(TodoFacts facts, double target) => facts.ZoneKm >= target;

    public TodoCycle Advance(TodoCycle cycle) =>
        cycle with { Target = cycle.Target + Math.Floor(cycle.Target / 2.0) };
}
