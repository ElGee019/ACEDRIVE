// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: Unity CarHaulJob delivery stamps + MapScreen's pickup, arrival
// and settle branches, PORTPLAN item 20.
namespace AceDrive.Core;

/// <summary>
/// One live haul: the drawn offer plus the leg stamps and phase transitions
/// Unity runs in MapScreen. Time is game minutes, never wall clock — Unity
/// stamps <c>GameClockManager.CurrentGameMinute()</c> and prices the delivery
/// leg from the difference, so a pause or a slow machine cannot inflate pay.
///
/// The run is the only writer of its phase, so the page cannot settle twice
/// or pick up at the wrong dealer: <see cref="PickUp"/> answers false unless
/// the job is still offered and the arrived door is this offer's dealer.
/// </summary>
public sealed class CarHaulRun
{
    public CarHaulRun(CarHaulOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        Offer = offer;
    }

    public CarHaulOffer Offer { get; }

    public CarHaulPhase Phase { get; private set; } = CarHaulPhase.Offered;

    /// <summary>The minute the delivery leg left the dealer, null before DRIVE.</summary>
    public long? DeliveryStartGameMinute { get; private set; }

    /// <summary>The minute the customer arrival fired, null before delivery.</summary>
    public long? DeliveryEndGameMinute { get; private set; }

    /// <summary>
    /// True when the arrived dealer door is this haul's dealer and the car has
    /// not been picked up yet. Unity's <c>MatchesDealerPickup</c>.
    /// </summary>
    public bool MatchesDealerPickup(string dealerId) =>
        Phase == CarHaulPhase.Offered
        && !string.IsNullOrEmpty(dealerId)
        && string.Equals(dealerId, Offer.Dealer.Id, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// PICK UP at the dealer: arms the delivery leg. False at any other door
    /// or when the car already left the dealer, so the map keeps treating the
    /// door as a normal dealership.
    /// </summary>
    public bool PickUp(string dealerId)
    {
        if (!MatchesDealerPickup(dealerId))
        {
            return false;
        }

        Phase = CarHaulPhase.PickedUp;
        return true;
    }

    /// <summary>
    /// DRIVE on the customer marker: stamps the delivery leg's start. False
    /// before pickup or on a second DRIVE tap, so a re-entered map cannot
    /// reset the clock and shrink the pay.
    /// </summary>
    public bool BeginDelivery(long gameMinute)
    {
        if (Phase != CarHaulPhase.PickedUp || DeliveryStartGameMinute is not null)
        {
            return false;
        }

        DeliveryStartGameMinute = gameMinute;
        return true;
    }

    /// <summary>
    /// DELIVER THE CAR at the customer: closes the run. Delivery succeeds even
    /// without a start stamp (an EVO session that lost its clock still ends the
    /// job, paying 0), matching Unity's status-first settle.
    /// </summary>
    public bool Deliver(long gameMinute)
    {
        if (Phase != CarHaulPhase.PickedUp)
        {
            return false;
        }

        DeliveryEndGameMinute = gameMinute;
        Phase = CarHaulPhase.Delivered;
        return true;
    }

    /// <summary>
    /// The paid delivery-leg length. Zero while the leg is unstarted or the
    /// stamps are out of order, exactly Unity's
    /// <c>end &gt; start &amp;&amp; start &gt; 0</c> guard: a negative or
    /// clockless run earns nothing instead of throwing.
    /// </summary>
    public int DriveMinutes =>
        DeliveryStartGameMinute is { } start
        && DeliveryEndGameMinute is { } end
        && start > 0
        && end > start
            ? (int)(end - start)
            : 0;
}
