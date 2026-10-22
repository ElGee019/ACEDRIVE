// Taxi-shift persistence (PORTPLAN item 12, Unity TaxiShiftService): the open
// shift with its wall-clock stamps survives a restart, so its 30-minute idle
// rule keeps running while the overlay (and EVO) are closed.
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

public interface IShiftStore
{
    /// <summary>Saved shift, or null when there is nothing to resume.</summary>
    TaxiShiftSnapshot? Load();

    void Save(TaxiShiftSnapshot snapshot);
}
