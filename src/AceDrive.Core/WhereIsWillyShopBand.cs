// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: ToWhereIsWilly.md §2.5 / §3.2 (Unity
// WhereIsWillyHandler.CalculateReward's coffeeShopCount bands).
namespace AceDrive.Core;

/// <summary>
/// One coffee-shop-count band of the finder's fee: the highest shop count it
/// covers plus the multiplier it pays. <see cref="MaxShops"/> is null on the
/// open-ended last band, so the balance file can grow a band without a code
/// change and a town of any size always resolves.
/// </summary>
public sealed record WhereIsWillyShopBand(int? MaxShops, double Multiplier);
