// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (Unity
// HubCareerCatalog: hub names, per-delivery CredP ramp, owner pool).
namespace AceDrive.Core;

/// <summary>Balance/content table for the hub page. Loaded from
/// Data/delivery_hubs.json at startup; this Default is the test-shell
/// fallback (no careers openable), not the source of truth. Ramp and owner
/// pool fall back to Unity's numbers/pool when the JSON omits them, so
/// hand-written catalogs and tests stay valid without repeating balance.</summary>
public sealed record HubCatalog(
    IReadOnlyList<HubTier> Tiers,
    CredPRamp? Ramp = null,
    IReadOnlyList<string>? OwnerNames = null)
{
    public static readonly HubCatalog Default = new([]);

    /// <summary>The per-delivery CredP ramp actually in force.</summary>
    public CredPRamp RampInForce => Ramp ?? CredPRamp.UnityDefault;

    /// <summary>The hub owner pool actually in force; empty means "no owner
    /// assigned", which the receipts hide.</summary>
    public IReadOnlyList<string> OwnersInForce => OwnerNames ?? [];
}
