// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11 (Unity
// TaxiReceiptScreen.PopulateFare + DeliveryReceiptScreen.PopulateFare —
// both walk their table cutting the driven distance into per-tier km).
namespace AceDrive.Core;

/// <summary>
/// Per-tier breakdown of a driven distance against a fare table: only the
/// tiers the ride actually reaches produce a line, like the Unity screens
/// hiding unused rows. One place so every receipt's money paid and its
/// displayed breakdown can never drift apart (Unity keeps its two copies
/// deliberately in one place; we keep one copy, period).
/// </summary>
public static class FareTierLines
{
    public static List<ReceiptTier> Of(double distanceKm, IReadOnlyList<FareTier> tiers, int capacity = 1)
    {
        List<ReceiptTier> lines = [];
        double remaining = Math.Max(0.0, distanceKm);
        double start = 0.0;
        foreach (FareTier tier in tiers)
        {
            if (remaining <= 0.0)
            {
                break;
            }
            double km = tier.WidthKm <= 0.0 ? remaining : Math.Min(remaining, tier.WidthKm);
            // Binary residue (8.2 − 2 − 3 = 3.1999…): the page shows one
            // decimal, and the record must compare exact in tests.
            km = Math.Round(km, 6);
            lines.Add(new ReceiptTier(
                start, tier.WidthKm, tier.RatePerKm, km,
                Math.Round(km * tier.RatePerKm * capacity, 2, MidpointRounding.AwayFromZero)));
            remaining -= km;
            start += tier.WidthKm;
        }
        return lines;
    }
}
