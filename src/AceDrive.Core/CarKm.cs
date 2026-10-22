// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 35 (car rarity) and LATESTDRIVEBS2
// CarRarityService: global km per car, ranked across all players.
namespace AceDrive.Core;

/// <summary>One car's all-players km total, as the rarity board sees it.</summary>
public readonly record struct CarKm(string Key, int Km);
