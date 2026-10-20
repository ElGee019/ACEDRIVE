// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 16-19
// (Delivery Job page, port of Unity DeliveryJobScreen + DeliveryCartAccounting).
namespace AceDrive.Core;

/// <summary>
/// A staged cart run handed over at DELIVER: which stores the cart held and
/// the money it moved — wholesale paid out for the goods and the retail
/// payout they come back with. Both sums are computed on the live
/// pre-restock shelves: what the delivery actually carries is the need the
/// goods fill.
/// </summary>
public sealed record DeliveryRun(IReadOnlyList<string> StoreKeys, double PaidForGoods, double ReceivedForGoods);
