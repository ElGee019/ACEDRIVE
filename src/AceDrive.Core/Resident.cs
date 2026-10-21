// One persisted homeowner on the address map (PORTPLAN items 11/24/45,
// port of Unity TaxiJobResidentService's resident POI). Pure data: the
// resident book owns identity reuse, the proposal page formats it.
namespace AceDrive.Core;

/// <summary>
/// A named resident at one address. Unity stores the owner name and the
/// companion suffix on the POI the first time an address is drawn, so a
/// later job at the same door meets the same person.
/// </summary>
public sealed record Resident(AddressLocation Address, ProceduralName Name);
