// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (wholesale
// hub ownership + credibility/CredP ramp), port of Unity HubCareerCatalog.
namespace AceDrive.Core;

/// <summary>One purchasable hub inside a career column (Unity HubLevel):
/// a proper name plus the one-time CredP price.</summary>
public sealed record HubLevel(string Name, double Cost);
