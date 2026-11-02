// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: Unity CarHaulOfferService.TryBuildVehicleOffer (the loyalty
// gate), PORTPLAN item 20.
namespace AceDrive.Core;

/// <summary>
/// One draw's outcome: the offer, or nothing plus the reason the caller may
/// name. <see cref="BlockedByBrandLoyalty"/> is the only refusal the player can
/// act on (drive the brand), so the page says so; the other empty-pool cases
/// (no car of the drawn brand, no dealer door, no customer door) stay one
/// generic line because they describe missing world data, not progress.
/// </summary>
public sealed record CarHaulDraw(CarHaulOffer? Offer, bool BlockedByBrandLoyalty);
