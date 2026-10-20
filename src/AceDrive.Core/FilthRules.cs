// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 33, port of
// Unity Utils/CarWashService.cs (MaxFilth 25, flat CR50 wash) and the
// MapScreen HUD's amber band.
namespace AceDrive.Core;

public sealed record FilthRules(int MaxFilth, int WashPrice)
{
    /// <summary>Unity's shipped CarWashService (MaxFilth 25, WashCost 50).
    /// Fallback for tests and for a missing or invalid Data/filth_rules.json —
    /// the loaded file is the source of truth.</summary>
    public static readonly FilthRules Default = new(
        MaxFilth: 25,
        WashPrice: 50);

    /// <summary>True once another taxi pickup is refused (Unity
    /// TaxiJobLifecycleHandler's dirty check: GetCarFilth &gt;= 25).</summary>
    public bool RefusesPickup(int filth) => filth >= MaxFilth;

    // Amber band of the filth HUD line, ported from Unity MapScreen.
    // UpdateFilthDisplay: filth >= 15 turns the reading orange until it
    // reaches the cap (the cap paints the shared mint). Display data
    // (MapScreen), not economy balance — no ctor parameter, no JSON
    // round-trip, exactly like the hunger amber band.
    public const int AmberFrom = 15;

    /// <summary>Whether a reading wears Unity's amber HUD colour (15 up to
    /// the cap; the cap itself paints the "fully filthy" mint).</summary>
    public bool IsAmber(int filth) => filth >= AmberFrom && filth < MaxFilth;
}
