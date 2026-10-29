// Pure gameplay data for the Saturday Touristenfahrten delivery run.
namespace AceDrive.Core;

/// <summary>
/// One item line of a drawn order: the item, the resx key that names it, how
/// many units the shop loaded and the unit price the receipt is priced from.
/// Quantity-zero lines can exist while the draw grows an order to its minimum.
/// </summary>
public sealed record TouristenOrderLine(
    string Key, string NameKey, int Quantity, double UnitPrice);
