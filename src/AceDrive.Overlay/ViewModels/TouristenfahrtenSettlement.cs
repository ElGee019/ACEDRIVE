using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Prices and banks one delivered Touristenfahrten dropoff: the order's value
/// is the goods payout, the drawn leg's distance adds the same tiered delivery
/// fare the career runs charge, and the goods cost the courier nothing because
/// the shop fronts them. Then it shows the shared delivery receipt page (the
/// same page, wallet and XP lines the delivery board settles with).
///
/// The XP pair comes from <see cref="DeliveryCarFacts"/>, so a run in an
/// unowned free-roam car pays the cash but earns no XP, exactly like a career
/// delivery.
/// </summary>
public sealed class TouristenfahrtenSettlement : ITouristenSettlement
{
    private readonly DriverWallet _wallet;
    private readonly DeliveryFareTable _fares;
    private readonly FleetService _fleet;
    private readonly ICarRaritySource _rarity;
    private readonly IRivalsService _rivals;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    public TouristenfahrtenSettlement(
        DriverWallet wallet, DeliveryFareTable fares, FleetService fleet,
        ICarRaritySource rarity, IRivalsService rivals, VoiceBeat beat, ILog log)
    {
        _wallet = wallet;
        _fares = fares;
        _fleet = fleet;
        _rarity = rarity;
        _rivals = rivals;
        _beat = beat;
        _log = log;
    }

    /// <summary>
    /// Banks one dropoff and shows its receipt. <paramref name="distanceKm"/>
    /// is the planned leg (pickup or previous stop to this one) measured on the
    /// address map; <paramref name="continueAction"/> is where the receipt's
    /// CONTINUE goes — the next dropoff or the calendar.
    /// </summary>
    public void Settle(
        TouristenDropoff dropoff, double distanceKm,
        ITouristenfahrtenHost host, Action continueAction)
    {
        ArgumentNullException.ThrowIfNull(dropoff);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(continueAction);

        (int carClass, int carRarity) = DeliveryCarFacts.Of(_fleet, _rarity);
        DeliveryReceipt receipt = DeliveryReceiptBuilder.Build(
            distanceKm,
            paidForGoods: 0.0,
            receivedForGoods: dropoff.Order.Total,
            _fares,
            carClass,
            carRarity,
            JobSettlement.PlaceholderStreetKnowledgePct,
            _wallet.Xp,
            awardsXp: !_fleet.IsDrivingUnownedCar());
        _wallet.AddDelivery(receipt);
        host.RefreshStandings();
        _log.LogInfo(
            $"touristenfahrten delivered: {dropoff.Door.Name}, " +
            $"{dropoff.Order.ItemCount} item(s), goods {dropoff.Order.Total:0.00}, " +
            $"dist={receipt.DistanceKm:0.00}km fare={receipt.DistanceCharge:0.00} " +
            $"earned={receipt.TotalEarned:0.00} xp={receipt.XpEarned:0.00} " +
            $"wallet={_wallet.Cash:0.00}");
        host.ShowReceipt(new DeliveryReceiptScreenViewModel(
            receipt, _wallet, _beat, new RelayCommand(continueAction), _rivals));
    }
}
