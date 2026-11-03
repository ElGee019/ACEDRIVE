// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (Unity
// HubCareerCatalog: hub names, per-delivery CredP ramp).
namespace AceDrive.Core;

/// <summary>Balance/content table for the hub page. Loaded from
/// Data/delivery_hubs.json at startup; this Default is the test-shell
/// fallback (no careers openable), not the source of truth. Ramp falls back
/// to Unity's numbers when the JSON omits it, so hand-written catalogs and
/// tests stay valid without repeating balance. The owner person is not
/// content: <see cref="DeliveryCareer"/> draws it from the cultural name pool
/// at purchase, the same draw every other person gets.</summary>
public sealed record HubCatalog(
    IReadOnlyList<HubTier> Tiers,
    CredPRamp? Ramp = null)
{
    public static readonly HubCatalog Default = new([]);

    /// <summary>The per-delivery CredP ramp actually in force.</summary>
    public CredPRamp RampInForce => Ramp ?? CredPRamp.UnityDefault;
}
