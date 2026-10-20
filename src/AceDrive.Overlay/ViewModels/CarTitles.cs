using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The one place the game's car naming is spelled: year, brand and model, all
/// caps ("1990 MAZDA MX-5 NA"). The parts are proper nouns and the year is a
/// number, so nothing here is translated — hence a helper and not a resx key.
/// A car the catalogue no longer lists falls back to its raw key so a retired
/// variant still reads as something on the garage page.
/// </summary>
public static class CarTitles
{
    public static string Of(CarSpec car) => $"{car.Year} {car.Brand} {car.Model}".ToUpperInvariant();

    public static string Of(string key, CarSpec? spec) =>
        spec is null ? key.ToUpperInvariant() : Of(spec);
}
