// Pure gameplay content for the Saturday Touristenfahrten delivery run.
namespace AceDrive.Core;

/// <summary>
/// One place the run may deliver to (the Nordschleife corners around the
/// Eifel): the stable door id the POI directory banks it under and the name
/// the companion prints. The five rows live in Data/touristenfahrten.json.
/// </summary>
public sealed record TouristenDropoffDefinition(string Id, string Name);
