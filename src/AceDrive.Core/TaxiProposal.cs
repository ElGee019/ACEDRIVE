// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11, Unity
// TaxiJobGenerator steps 3/4/6.
namespace AceDrive.Core;

/// <summary>
/// One generated job proposal: the passenger resident, the resident of the
/// second address (their surname names "the X residence"), the coin flip
/// deciding whether the customer's home is the pickup or the drop-off leg
/// (Unity's isCustomerHomePickup), and the two doors themselves. Residents
/// are reused per address through <see cref="ResidentBook"/>, like Unity's
/// persisted resident POIs.
/// </summary>
public sealed record TaxiProposal(
    ProceduralName Customer,
    ProceduralName SecondResident,
    bool HomeIsPickup,
    AddressLocation? CustomerHome = null,
    AddressLocation? SecondResidence = null);
