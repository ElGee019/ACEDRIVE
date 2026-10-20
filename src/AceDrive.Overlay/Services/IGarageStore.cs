// Garage persistence (PORTPLAN item 30): the owned cars and the current car, so a
// restart reopens the garage the player left instead of an empty one. Its own file
// beside driver.json — the garage is a list of records, the driver a fixed set of
// scalars (the brand_mileage.json precedent).
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

public interface IGarageStore
{
    /// <summary>Saved garage, or null when there is nothing to resume.</summary>
    GarageState? Load();

    void Save(GarageState state);
}
