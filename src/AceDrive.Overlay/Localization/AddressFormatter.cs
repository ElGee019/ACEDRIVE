using AceDrive.Core;

namespace AceDrive.Overlay.Localization;

/// <summary>
/// The one address shape shared by the HUD, the errand dialogue and future
/// job copy. Lookup follows the UI culture; numbers follow the formatting
/// culture.
/// </summary>
public static class AddressFormatter
{
    public static string Format(AddressLocation address) =>
        Format(address.StreetName, address.HouseNumber);

    /// <summary>
    /// The same shape from a street and number already held apart (a receipt
    /// that stores the two fields rather than a resolved location).
    /// </summary>
    public static string Format(string streetName, int houseNumber) =>
        Strings.Format("HudAddress", streetName, houseNumber);
}
