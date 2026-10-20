// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 48, Unity
// BankInterestHelper + BankScript + BankGenerator.
namespace AceDrive.Core;

/// <summary>
/// The savings bank's rules: the interest rate is
/// <c>discoveredStreets × daysPlayedInARow</c> and the credited amount
/// compounds on the bank balance. The minimum saldo locks a floor on the
/// account — withdrawals only ever move money above it into cash. The
/// placement constants belong to the marker network (items 24/59) and sit
/// here so the bank POI gate is data-free and testable before that lands.
/// </summary>
public static class BankRules
{
    /// <summary>The locked floor: withdrawals stop above this, Unity's
    /// "minimum saldo 1000".</summary>
    public const double MinBankSaldo = 1000.0;

    /// <summary>First bank POI spawns at 15 discovered streets (Unity
    /// BankGenerator's FirstBankStreetCount).</summary>
    public const int FirstBankStreetCount = 15;

    /// <summary>Then one bank POI per 51 further streets (Unity
    /// BankGenerator's increments of 51).</summary>
    public const int BankIntervalStreets = 51;

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

    /// <summary>The street count that must be crossed before the n-th bank
    /// POI can spawn (Unity BankGenerator): 15 for the first, +51 per place
    /// after. Dormant until the marker network lands; the constant keeps the
    /// gate out of generator code when it does.</summary>
    public static int RequiredStreetCountFor(int existingBankCount) =>
        FirstBankStreetCount + BankIntervalStreets * Math.Max(0, existingBankCount);
}
