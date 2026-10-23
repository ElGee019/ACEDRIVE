// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 48, Unity
// BankInterestHelper + BankScript + BankGenerator.
namespace AceDrive.Core;

/// <summary>
/// The savings bank's rules: the interest rate is
/// <c>discoveredStreets × daysPlayedInARow</c> and the credited amount
/// compounds on the bank balance. The minimum saldo locks a floor on the
/// account — withdrawals only ever move money above it into cash.
/// </summary>
public static class BankRules
{
    /// <summary>The locked floor: withdrawals stop above this, Unity's
    /// "minimum saldo 1000".</summary>
    public const double MinBankSaldo = 1000.0;

    /// <summary>Unity's <c>/ 10000</c> divisor on the interest product.</summary>
    public const double InterestDivisor = 10000.0;

    /// <summary>Unity's interest rate = streets × consecutive days.</summary>
    public static int Rate(int streetCount, int daysInARow) =>
        Math.Max(0, streetCount) * Math.Max(0, daysInARow);

    /// <summary>The CR credited into the bank at one settle: bank × rate /
    /// 10000 (Unity BankInterestHelper.ComputeBankInterest — float math
    /// there; the wallet rounds at its own boundary so the ledger stays
    /// 2-decimal).</summary>
    public static double ComputeInterest(double bankAmount, int streetCount, int daysInARow) =>
        Math.Max(0.0, bankAmount) * Rate(streetCount, daysInARow) / InterestDivisor;

    /// <summary>The largest amount a withdrawal may move (Unity's
    /// maxWithdraw guard): money above the saldo floor.</summary>
    public static double MaxWithdraw(double bankAmount) =>
        Math.Max(0.0, bankAmount - MinBankSaldo);
}
