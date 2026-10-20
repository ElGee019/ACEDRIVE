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
        Strings.Format("HudAddress", address.StreetName, address.HouseNumber);
}
