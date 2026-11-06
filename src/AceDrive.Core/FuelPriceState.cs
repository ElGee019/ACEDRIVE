// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The persisted half of the fuel state (PORTPLAN items 37/38): the CR/L the
// market last rolled to, the game day it rolled on, and the roadside refuel's
// paid-for tank, so a restart continues the same drift instead of re-seeding
// Unity's 1.65 and does not lose the fill a repair already bought.
namespace AceDrive.Core;

/// <summary>One saved fuel reading (Unity PlayerData.fuelPrice +
/// GameClockManager.lastPriceUpdateDay) plus the roadside refuel's paid-for
/// tank (PORTPLAN 38): the litres a repair already bought and the car they
/// belong to. Both ride the same file so a restart between the repair page
/// and the next fill neither forgets the credit nor applies it to another
/// car.</summary>
public sealed record FuelPriceState(
    double PricePerLitre,
    int LastRollDay,
    double PrepaidLitres = 0.0,
    string? PrepaidCarKey = null);
