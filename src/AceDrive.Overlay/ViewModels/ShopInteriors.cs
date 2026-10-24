using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The five shop interiors (PORTPLAN items 29/33/34/48, Unity's
/// BankScript/CarDealerScreen/CarWashScreen/CarMechanicScreen/InsuranceScreen).
/// Each page never writes: it reports what the player did and the host banks
/// the shared saves here, through <see cref="GameStateSaver"/>.
///
/// Assembly only: the BACK command and the changed/on-bought callbacks come
/// from the caller, so this knows nothing about navigation or which page opened
/// the shop.
/// </summary>
public sealed class ShopInteriors
{
    private readonly DriverWallet _wallet;
    private readonly Garage _garage;
    private readonly GameClock _game;
    private readonly InsuranceRules _insurance;
    private readonly IReadOnlyList<CarSpec> _cars;
    private readonly GameStateSaver _saver;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    public ShopInteriors(
        DriverWallet wallet, Garage garage, GameClock game, InsuranceRules insurance,
        IReadOnlyList<CarSpec> cars, GameStateSaver saver, VoiceBeat beat, ILog log)
    {
        _wallet = wallet;
        _garage = garage;
        _game = game;
        _insurance = insurance;
        _cars = cars;
        _saver = saver;
        _beat = beat;
        _log = log;
    }

    // The bank interior (PORTPLAN 48, Unity BankScript.Show): the saldo opens
    // live. The page never writes; a withdraw routes through the host save so
    // the saldo never forks.
    public BankScreenViewModel Bank(ICommand back, Action onChanged) =>
        new BankScreenViewModel(
            _wallet, back,
            moved =>
            {
                onChanged();
                _log.LogInfo($"[Bank] withdrew {moved:0.00} CR " +
                    $"(cash {_wallet.Cash:0.00}, bank {_wallet.BankBalance:0.00})");
            },
            _beat);

    // The dealer's weekly floor (PORTPLAN 29, Unity CarDealerScreen). The week
    // rides in from the display week, so the floor turns over with the calendar
    // and a re-opened page inside one week shows the same cars (DealerStock).
    // The bought-car choice is the host's page (onBought).
    public DealerScreenViewModel Dealer(
        ICommand back, int displayWeek, Action<CarSpec> onBought) =>
        new(_cars, _garage, _wallet, displayWeek, back, onBought, _beat);

    // The car wash (PORTPLAN 33, Unity CarWashScreen): a fresh page per visit,
    // so the car driven now and its reading are live. A wash spends the flat
    // price and clears the car's filth; both are banked here (the driver save
    // through onChanged for the cash, garage.json for the reading) and logged.
    public CarWashScreenViewModel CarWash(ICommand back, Action onChanged) =>
        new CarWashScreenViewModel(
            _garage, _wallet, _beat, back,
            (removed, paid) =>
            {
                onChanged();
                _saver.SaveGarage();
                _log.LogInfo($"[CarWash] removed {removed} filth for {paid:0.00} CR " +
                    $"(cash {_wallet.Cash:0.00}, {_garage.CurrentKey ?? "no car"})");
            });

    // The car mechanic (PORTPLAN items 31/32, Unity CarMechanicScreen): a fresh
    // page per visit, so the car driven now, its wear deck and its engine are
    // live. Parts are replaced, the engine repaired or swapped; every action
    // moves cash and mutates the car's condition, so both are banked here (the
    // driver save through onChanged for the cash, garage.json for the
    // condition) and logged.
    public MechanicScreenViewModel Mechanic(ICommand back, Action onChanged) =>
        new MechanicScreenViewModel(
            _garage, _wallet, _game, _beat, back,
            repair =>
            {
                // A covered car's repair pays 90% into the escrow the broker
                // collects (Unity RepairScreen's insurance tail, repurposed to
                // the mechanic since ACE owns accidents). The page never
                // writes; the host banks the cash and the condition.
                if (_garage.Current is { HasInsurance: true } insured && repair.Cost > 0.0)
                {
                    _wallet.AddInsurancePayout(repair.Cost * 0.9);
                }
                onChanged();
                _saver.SaveGarage();
                _log.LogInfo($"[Mechanic] {repair.Kind} cost {repair.Cost:0.00} CR " +
                    $"(parts {repair.PartsReplaced}, engine replaced {repair.EngineReplaced}, " +
                    $"cash {_wallet.Cash:0.00}, {_garage.CurrentKey ?? "no car"})");
            });

    // Insurance broker (PORTPLAN 34, Unity InsuranceScreen): a fresh page per
    // visit, so the cover flags and the escrow are live. A toggle and a
    // collection are banked here (the garage for the flag, the driver through
    // onChanged for the cash + escrow) and logged.
    public InsuranceScreenViewModel Insurance(ICommand back, Action onChanged) =>
        new InsuranceScreenViewModel(
            _garage, _wallet, _insurance, _wallet.Rating.Overall, _beat, back,
            (key, insured) =>
            {
                _saver.SaveGarage();
                _log.LogInfo($"[Insurance] {key} {(insured ? "insured" : "uninsured")} " +
                    $"(cash {_wallet.Cash:0.00})");
            },
            moved =>
            {
                onChanged();
                _log.LogInfo($"[Insurance] collected {moved:0.00} CR (cash {_wallet.Cash:0.00})");
            });
}
