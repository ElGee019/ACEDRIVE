// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (wholesale
// hub ownership + credibility/CredP ramp), port of Unity HubCareerCatalog.
namespace AceDrive.Core;

/// <summary>Balance/content table for the hub page. Loaded from
/// Data/delivery_hubs.json at startup; this Default is the test-shell
/// fallback (no careers openable), not the source of truth.</summary>
public sealed record HubCatalog(IReadOnlyList<HubTier> Tiers)
{
    public static readonly HubCatalog Default = new([]);
}
