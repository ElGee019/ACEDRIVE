// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The persisted half of the pump price (PORTPLAN item 37): the CR/L the market
// last rolled to and the game day it rolled on, so a restart continues the
// same drift instead of re-seeding Unity's 1.65.
namespace AceDrive.Core;

/// <summary>One saved fuel-market reading (Unity PlayerData.fuelPrice +
/// GameClockManager.lastPriceUpdateDay).</summary>
public sealed record FuelPriceState(double PricePerLitre, int LastRollDay);
