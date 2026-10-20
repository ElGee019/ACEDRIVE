// Balance table for car insurance (PORTPLAN item 34), port of Unity
// Utils/InsurancePricing.cs + GameClockManager.InsurancePremiumRate /
// UpdateInsurance: the daily rate, the general increase and the CR floor, plus
// the game hour the nightly charge runs. Data/insurance_rules.json is the
// source of truth; Default is Unity's shipped config (rate 0.0005, +15%, CR1,
// 01:00) and the fallback for tests and a missing/invalid file.
namespace AceDrive.Core;

public sealed record InsuranceRules(
    double PremiumRate,
    double BaseIncreaseFactor,
    double MinimumDailyCost,
    int ChargeHour)
{
    /// <summary>Unity's shipped config (GameTimeConfig.insurancePremiumRate
    /// 0.0005, InsurancePricing.BaseIncreaseFactor 1.15, MinimumDailyCost 1,
    /// the charge firing at game hour 01).</summary>
    public static readonly InsuranceRules Default = new(
        PremiumRate: 0.0005,
        BaseIncreaseFactor: 1.15,
        MinimumDailyCost: 1.0,
        ChargeHour: 1);
}
