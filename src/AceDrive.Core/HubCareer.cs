// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (wholesale
// hub ownership + credibility/CredP ramp), port of Unity HubCareerCatalog
// + DeliveryHubScreen.
namespace AceDrive.Core;

/// <summary>A delivery career: one product category with its hub levels
/// (Unity CategoryDef). The hub screen draws each career as a column, and
/// owning any hub of the category connects that category's stores to the
/// delivery board.</summary>
public sealed record HubCareer(string Id, string Label, IReadOnlyList<HubLevel> Levels);
