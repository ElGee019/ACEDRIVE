// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: Unity CarHaulJob (Models/CarHaulJob.cs) and the offer fields
// JobSelectionScreen builds from CarHaulOfferService.Offer, PORTPLAN item 20.
namespace AceDrive.Core;

/// <summary>
/// One drawn car-haul offer: the customer's brand-new car waiting at a
/// dealer, the dealer door to pick it up at, and the customer's home door,
/// name and the brand loyalty that opened the offer. Pure data — the page
/// formats it, the run owns its phases, the receipt prices the settled leg.
/// Unity's year fields are absent because ACEDRIVE has no year system.
/// </summary>
public sealed record CarHaulOffer(
    CarSpec Car,
    PointOfInterest Dealer,
    AddressLocation CustomerAddress,
    ProceduralName Customer,
    double BrandLoyaltyPercent)
{
    /// <summary>
    /// The haul's pay tier from the delivered car's production class, so the
    /// page can show what the job is worth before the drive. The settled
    /// receipt recomputes it from the same class.
    /// </summary>
    public int PayoutTier => CarHaulReceiptBuilder.TierForCarClass(Car.Class);

    /// <summary>The class window the loyalty percent opened the draw from.</summary>
    public CarHaulClassBand ClassBand => CarHaulClassBand.For(BrandLoyaltyPercent);
}
