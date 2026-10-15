// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 11, Unity
// TaxiJobGenerator steps 3/4/6.
namespace AceDrive.Core;

/// <summary>
/// One generated job proposal: the passenger resident, the resident of the
/// second address (their surname names "the X residence"), and the coin
/// flip deciding whether the customer's home is the pickup or the drop-off
/// leg (Unity's isCustomerHomePickup). Resident persistence — Unity reuses
/// the POI owner once a home is known — arrives with the POI network
/// (PORTPLAN 24); until then every proposal draws both residents fresh.
/// </summary>
public sealed record TaxiProposal(
    ProceduralName Customer,
    ProceduralName SecondResident,
    bool HomeIsPickup);
