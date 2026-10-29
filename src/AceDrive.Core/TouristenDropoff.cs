// Pure gameplay data for the Saturday Touristenfahrten delivery run.
namespace AceDrive.Core;

/// <summary>
/// One planned stop of a run: the landmark's door and the order it takes.
/// The door is a real POI (placed in the directory, so the companion can pin
/// its address); the order is drawn before the pickup so the whole run is
/// known the moment the player loads the goods.
/// </summary>
public sealed record TouristenDropoff(PointOfInterest Door, TouristenOrder Order);
