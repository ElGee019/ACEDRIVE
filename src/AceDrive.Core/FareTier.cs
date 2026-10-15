// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11 (Unity
// TaxiReceiptScreen.CalculateDistanceCharge tiers).
namespace AceDrive.Core;

/// <summary>
/// One fare tier: up to <see cref="WidthKm"/> km at <see cref="RatePerKm"/>
/// EUR/km. A non-positive width means "all remaining km" (the open top
/// tier), so the table needs no infinity literal in JSON.
/// </summary>
public sealed record FareTier(double WidthKm, double RatePerKm);
