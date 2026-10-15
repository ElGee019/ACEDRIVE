// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11, Unity
// TaxiJobGenerator steps 3/4/6.
namespace AceDrive.Core;

/// <summary>
/// Draws a whole proposal like Unity's TaxiJobGenerator: two procedural
/// residents from the name pools (the passenger at their home address and
/// the resident of the second address) plus the pickup/drop-off coin flip.
/// Seeded like the ledger and the name generator, so the proposal sequence
/// replays deterministically in tests.
/// </summary>
public sealed class TaxiJobProposer(NamePoolTable table, int seed)
{
    private readonly ProceduralNameGenerator _names = new(table, seed);

    public TaxiProposal Next() =>
        new(_names.Next(), _names.Next(), _names.NextCoinFlip());
}
