// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (wholesale
// hub ownership + credibility/CredP ramp), port of Unity HubCareerCatalog
// + DeliveryHubScreen.
namespace AceDrive.Core;

/// <summary>A tier row on the hub screen. Unity rule: disabled tiers keep
/// their button (and any data) but show COMING SOON columns.</summary>
public sealed record HubTier(int Id, bool Enabled, IReadOnlyList<HubCareer> Careers);
