// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/43 (Unity
// PlayerData.cash + PlayerData.xp accumulation on dropoff).
namespace AceDrive.Core;

/// <summary>
/// The driver's cash wallet + XP total. Every settled receipt lands here;
/// the store banks the snapshot. Cold paths only (dropoff, startup), so
/// rounding per receipt is free of the 10 Hz loop.
/// </summary>
public sealed class DriverWallet
{
    public DriverWallet(DriverState? saved = null)
    {
        Cash = Math.Max(0.0, saved?.Cash ?? 0.0);
        Xp = Math.Max(0.0, saved?.Xp ?? 0.0);
    }

    public double Cash { get; private set; }
    public double Xp { get; private set; }

    public void AddReceipt(TaxiReceipt receipt)
    {
        Cash = Math.Round(Cash + receipt.Total, 2, MidpointRounding.AwayFromZero);
        Xp += receipt.XpEarned;
    }

    public DriverState Snapshot() => new(Cash, Xp);
}
