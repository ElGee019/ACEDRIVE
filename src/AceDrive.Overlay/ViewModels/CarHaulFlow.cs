using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The car-hauling job (PORTPLAN item 20, Unity CarHaulingScreen): the third
/// job type, settle-only today.
///
/// With no driving slice the dealer-pickup / borrowed-leg / customer legs have
/// no inputs yet, so the placeholder haul is a fixed delivery leg that settles
/// the moment the tap lands and shows its receipt. The settle stays whole here
/// — the car's band, the rating behind the rate, the wallet and the checkpoint
/// bank — and grows its legs here, never as branches in the coordinator's
/// page path. The receipt is the flow's page like the taxi proposal and the
/// delivery board: the caller puts it up with its own BACK command, so no host
/// is needed and the flow stays a leaf.
/// </summary>
public sealed class CarHaulFlow
{
    private readonly ICarBrandSource _brands;
    private readonly IReadOnlyList<CarSpec> _cars;
    private readonly DriverWallet _wallet;
    private readonly DayCycle _day;
    private readonly GameStateSaver _saver;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    // Car-haul placeholder until the drive slice feeds the legs: a 30-minute
    // delivery leg (Unity stamps both leg ends in in-game minutes; the settle
    // prices the difference).
    private const int PlaceholderHaulMinutes = 30;

    public CarHaulFlow(
        ICarBrandSource brands, IReadOnlyList<CarSpec> cars, DriverWallet wallet,
        DayCycle day, GameStateSaver saver, VoiceBeat beat, ILog log)
    {
        _brands = brands;
        _cars = cars;
        _wallet = wallet;
        _day = day;
        _saver = saver;
        _beat = beat;
        _log = log;
    }

    /// <summary>
    /// Settles the placeholder haul straight into its receipt (the taxi
    /// ACCEPT skip's third use). Unity's receipt names the hauled car — a
    /// brand-new customer order waiting at a dealer; with no dealer or offers
    /// yet the customer's car is the one being driven now, and the pool row
    /// resolves its display name and real class band through the watcher's
    /// identity. The settled cash is progress the player can see, so the
    /// calendar is banked at once like the paid jobs' checkpoint.
    /// </summary>
    public CarHaulReceiptScreenViewModel Settle(ICommand backCommand)
    {
        CarSpec? car = _brands.CurrentIdentity?.FindIn(_cars);
        string carName = car is not null
            ? $"{car.Brand} {car.Model}".ToUpperInvariant()
            : Strings.Get("HaulReceiptCarUnknown");
        int carClass = car is { Class: > 0 } ? car.Class : JobSettlement.FallbackCarClass;
        if (car is { Class: > 0 })
        {
            _log.LogInfo($"haul car: {carName} class {carClass}");
        }
        else
        {
            // Class comes from the cars.json column; a row without one (or an
            // unknown car) keeps the placeholder band and says so.
            _log.LogInfo($"haul car: {carName} class {carClass} (placeholder, no pool class)");
        }

        CarHaulReceipt receipt = CarHaulReceiptBuilder.Build(
            carName, carClass, DriverRatingPercent, PlaceholderHaulMinutes);
        _wallet.AddHaul(receipt);
        _saver.SaveClock(_day.DisplayWeek);
        _log.LogInfo($"haul settled: {carName} minutes={receipt.DriveMinutes} " +
            $"tier={receipt.Tier} rate={receipt.RatePerMin:0.00}/min " +
            $"earned={receipt.TotalEarned:0.00} wallet={_wallet.Cash:0.00}");
        return new CarHaulReceiptScreenViewModel(receipt, _wallet, _beat, backCommand);
    }

    // The persisted skills average, rounded to the whole percent the haul
    // receipt and the fleet's insurance math take (Unity uses the float
    // overall directly; the receipt model is an int).
    private int DriverRatingPercent =>
        (int)Math.Round(_wallet.Rating.Overall, MidpointRounding.AwayFromZero);
}
