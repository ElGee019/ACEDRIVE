// The price/volume/trunk projection for a proposed shop purchase (PORTPLAN
// item 26, port of Unity MaxsScreen.UpdateSelectionPanel). One quote feeds
// both the selection readout and the buy gate, so they cannot drift.
namespace AceDrive.Core;

/// <summary>What the selection would cost and whether it can be bought.</summary>
public sealed record StorePurchaseQuote(
    StorePurchaseStatus Status,
    int Quantity,
    double TotalPrice,
    double TotalVolume,
    double ProjectedTrunkLitres);
