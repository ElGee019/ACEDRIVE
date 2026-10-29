// Pure gameplay data for the Saturday Touristenfahrten delivery run. No WPF,
// no I/O: the balance table is Data/touristenfahrten.json, this is its shape.
namespace AceDrive.Core;

/// <summary>
/// One item a pickup shop can load onto a Touristenfahrten order: the stable
/// key, the resx key that names it in a sentence, the price of one unit and
/// the quantity range a draw may roll. Power-of-two-free ranges are data, so a
/// new item is a row, never a branch.
/// </summary>
/// <param name="Key">Stable item key, also the order line's own key.</param>
/// <param name="NameKey">Resource key the order sentence names it with.</param>
/// <param name="Price">Price of one unit in euro.</param>
/// <param name="Min">Smallest quantity a single order may draw.</param>
/// <param name="Max">Largest quantity a single order may draw.</param>
public sealed record TouristenItemRule(
    string Key, string NameKey, double Price, int Min, int Max);
