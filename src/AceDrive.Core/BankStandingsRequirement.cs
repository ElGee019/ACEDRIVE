// "Have {target} CR in the bank." (PORTPLAN item 46, Unity
// BankStandingsEvaluator). The target rides the saldo floor plus a doubling
// interest allowance: 1002, 1004, 1008, ... so the bank's own interest is what
// keeps the subject reachable.
namespace AceDrive.Core;

public sealed class BankStandingsRequirement : ITodoRequirement
{
    public string Subject => TodoSubject.BankStandings;
    public bool IsOneTime => false;
    public double InitialTarget => BankRules.MinBankSaldo + 2.0;
    public string TipKey => "TodoBankStandings";
    public string? PoiType => null;
    public string? FallbackKey => null;

    public double Current(TodoFacts facts) => facts.Bank;

    public bool IsMet(TodoFacts facts, double target) => facts.Bank >= target;

    // Unity: interest = max(1, interest) * 2, target = 1000 + interest.
    public TodoCycle Advance(TodoCycle cycle)
    {
        int interest = Math.Max(1, cycle.BankInterest) * 2;
        return cycle with
        {
            BankInterest = interest,
            Target = BankRules.MinBankSaldo + interest,
        };
    }
}
