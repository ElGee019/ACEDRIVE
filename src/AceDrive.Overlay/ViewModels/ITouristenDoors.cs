using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The door side of the Saturday Touristenfahrten run: which shop loads the
/// goods and where each landmark stop lives. The implementation owns the POI
/// directory and banks the doors it opens; the flow only asks for doors, so a
/// fake substitutes cleanly in tests (LSP).
/// </summary>
public interface ITouristenDoors
{
    /// <summary>
    /// A known coffee shop or pizzeria on the map. When the career has not
    /// placed one yet, a fresh door of a random kind opens on the map and is
    /// banked, so the run always has a pickup. Null only when no address is
    /// free.
    /// </summary>
    PointOfInterest? Pickup();

    /// <summary>
    /// The landmark's own door, or a freshly placed one the first time the run
    /// visits it. Null when no free address exists.
    /// </summary>
    PointOfInterest? Dropoff(TouristenDropoffDefinition definition);
}
