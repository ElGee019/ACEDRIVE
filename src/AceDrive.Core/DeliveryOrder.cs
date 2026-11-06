// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>
/// One confirmed restock order: how many of a store's missing units the
/// player paid for and the money and trunk space that order moves. Units and
/// volume are frozen at the tap — the player's cash and the car's free litres
/// cap them — so the delivery raises exactly these units and leaves the rest
/// of the store's need for a later run.
/// </summary>
public sealed record DeliveryOrder(
    string StoreKey,
    int Units,
    double WholesaleCost,
    double RetailPayout,
    double Volume = 0.0);
