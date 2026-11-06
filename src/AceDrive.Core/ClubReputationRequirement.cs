// "Reach a club reputation of {target}." (PORTPLAN item 46, Unity
// ClubReputationEvaluator). The club's tier gates double from 1
// (cento_miglia.json's reputationRequired), so the tip doubles too: it asks for
// the first gate, then names each next gate the player is racing towards. The
// reputation itself only moves at a settled Cento Miglia, the one writer.
namespace AceDrive.Core;

public sealed class ClubReputationRequirement : ITodoRequirement
{
    public string Subject => TodoSubject.ClubReputation;
    public bool IsOneTime => false;
    public double InitialTarget => 1.0;
    public string TipKey => "TodoClubReputation";
    public string? PoiType => null;
    public string? FallbackKey => null;

    public double Current(TodoFacts facts) => facts.ClubReputation;

    public bool IsMet(TodoFacts facts, double target) => facts.ClubReputation >= target;

    public TodoCycle Advance(TodoCycle cycle) => cycle with { Target = cycle.Target * 2.0 };
}
