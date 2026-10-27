using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Prices and banks one finished Cento Miglia (PORTPLAN item 49, port of Unity
/// CentoMigliaScreen.ProcessReceiptRewards): the placement purse and any Star
/// of the Show prize into the wallet, the race's reputation reading into the
/// club level, the concentration points the place earns, and the generated
/// award/certificate into the car's trunk. Then it hands the receipt page to
/// the shell.
///
/// The race is solo until the NPC field lands, so the finishing place is first;
/// the receipt still carries the place so the field's arrival changes no page
/// and no rule but the placement input.
/// </summary>
public sealed class CentoRaceSettlement
{
    // Unity stores the award in the glove compartment (no capacity). The port's
    // glove compartment is a section of the trunk page over the same carried
    // list, so the stack still has to fit somewhere: a tenth of a litre keeps
    // it out of the food's way, and the page files it under the compartment
    // because the catalogue defines no bite for it.
    private const double ItemVolumeLitres = 0.1;
    private const int SoloPlacement = 1;

    private readonly DriverWallet _wallet;
    private readonly Garage _garage;
    private readonly Trunk _trunk;
    private readonly CentoRaceRules _rules;
    private readonly Random _rng;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    public CentoRaceSettlement(
        DriverWallet wallet, Garage garage, Trunk trunk, CentoRaceRules rules,
        Random rng, VoiceBeat beat, ILog log)
    {
        _wallet = wallet;
        _garage = garage;
        _trunk = trunk;
        _rules = rules;
        _rng = rng;
        _beat = beat;
        _log = log;
    }

    /// <summary>
    /// Banks the finished race and shows its receipt. The reputation pair is the
    /// ticker's start/end reading; the car that ran is the garage's current one,
    /// the same source the event page counted from.
    /// </summary>
    public void Settle(
        CentoMigliaEvent race, double startReputation, double endReputation,
        IJobSettlementHost host)
    {
        ArgumentNullException.ThrowIfNull(race);
        ArgumentNullException.ThrowIfNull(host);

        CarSpec? car = _garage.Current is { } owned ? _garage.SpecOf(owned) : null;
        string carName = _garage.Current is { } current
            ? CarTitles.Of(current.Key, car)
            : string.Empty;
        CentoReceipt receipt = CentoReceiptBuilder.Build(
            race, SoloPlacement, startReputation, endReputation, carName,
            car?.Class ?? 0, _garage.Cars.Count, _wallet.StarOfTheShowWins,
            _rules, _rng);
        _wallet.AddCentoRace(receipt);
        _wallet.AddConcentration(receipt.ConcentrationAward);
        if (receipt.ItemName.Length > 0)
        {
            _trunk.Add(receipt.ItemName, 1, ItemVolumeLitres);
        }

        // The driver file carries the wallet, the trunk and the win total, so
        // one bank keeps the race's whole outcome together (a kill right after
        // the receipt costs nothing).
        host.SaveDriver(standings: null);
        host.RefreshJob();
        _log.LogInfo(
            $"cento miglia settled: tier {race.TierNumber}, place {receipt.Placement}, " +
            $"purse {receipt.PlacementEarnings:0.00} + star {receipt.StarPrize:0.00}, " +
            $"rep {receipt.StartReputation:0.00000} -> {receipt.EndReputation:0.00000}, " +
            $"wins {receipt.StarWins}, item '{receipt.ItemName}', " +
            $"wallet {_wallet.Cash:0.00}");
        host.ShowReceipt(new CentoReceiptScreenViewModel(
            receipt, _wallet, _beat, new RelayCommand(host.GoToMenuPage)));
    }
}
