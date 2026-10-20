// Brand mileage storage (PORTPLAN item 35, Unity
// PlayerData.brandDistanceDriven): lifetime km per driven brand, keyed
// case-insensitively on the brand name. Small on purpose so fakes
// substitute cleanly in tests; the JSON file owns persistence.
namespace AceDrive.Overlay.Services;

public interface IBrandMileageStore
{
    /// <summary>Lifetime km driven in the brand's cars, 0 when never driven.</summary>
    double GetKm(string brand);

    /// <summary>
    /// Banks one completed ride and returns the new lifetime total for the
    /// receipt's sponsorship rate. Never throws (the file sink swallows IO).
    /// </summary>
    double RegisterRide(string brand, double distanceKm);
}
