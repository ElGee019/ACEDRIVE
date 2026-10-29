using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Prices and banks one finished road test (PORTPLAN item 49, port of Unity
/// RoadTestScreen.PopulateReceipt): the crowd readings, the shoot's variety
/// against the last one, the car journalist's reputation, the magazine price and
/// the earnings into the wallet, the settled record into the road-test ledger
/// (which also rotates the pick, Unity's MarkCurrentAsTested), and the two files
/// that carry them. Then it returns the receipt page the flow reveals.
///
/// WHY it is its own collaborator: the flow owns the phases and the clock; the
/// money and the persisted readings are a second responsibility with its own
/// writes, exactly like the Cento Miglia settle.
/// </summary>
public sealed class RoadTestSettlement
{
    private readonly DriverWallet _wallet;
    private readonly RoadTestLedger _roads;
    private readonly RoadTestAudienceSource _audience;
    private readonly RoadTestAudienceRules _rules;
    private readonly GameStateSaver _saver;
    private readonly IRivalsService _rivals;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    public RoadTestSettlement(
        DriverWallet wallet, RoadTestLedger roads, RoadTestAudienceSource audience,
        RoadTestAudienceRules rules, GameStateSaver saver, IRivalsService rivals,
        VoiceBeat beat, ILog log)
    {
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(roads);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(saver);
        ArgumentNullException.ThrowIfNull(rivals);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(log);
        _wallet = wallet;
        _roads = roads;
        _audience = audience;
        _rules = rules;
        _saver = saver;
        _rivals = rivals;
        _beat = beat;
        _log = log;
    }

    /// <summary>
    /// Settles the shoot and returns its receipt page, already bound to
    /// <paramref name="continueCommand"/>. Unity's order is kept: the car is
    /// marked tested (which raises the natural reputation and moves the pick)
    /// before the formula reads that reputation.
    /// </summary>
    public RoadTestReceiptScreenViewModel Settle(
        RoadTestShot shot, ICommand continueCommand)
    {
        ArgumentNullException.ThrowIfNull(shot);
        ArgumentNullException.ThrowIfNull(continueCommand);

        _roads.MarkTested();
        RoadTestReceipt receipt = RoadTestReceiptBuilder.Build(
            shot, _audience.Count(), _roads.Record, _rules);
        _roads.RecordReceipt(receipt);
        _wallet.AddRoadTest(receipt);
        // driver.json carries the cash; roadtest.json the settled record with
        // the rotated pick. Both writes happen here, once per receipt, so a
        // kill straight after the page still reopens what it paid.
        _saver.SaveDriver(_rivals.LastResult);
        _saver.SaveRoadTest();

        _log.LogInfo(
            $"road test settled: {shot.CarName} at {shot.Street} {shot.HouseNumber}, " +
            $"people {receipt.PeopleReached} (was {receipt.PreviousPeopleReached}), " +
            $"variety {receipt.Variety * 100.0:0.00}%, " +
            $"journalist {receipt.JournalistReputation:0.00}, " +
            $"magazine {receipt.MagazinePrice:0.00}, " +
            $"earnings {receipt.Earnings:0.00} (was {receipt.PreviousEarnings:0.00}), " +
            $"wallet {_wallet.Cash:0.00}");

        return new RoadTestReceiptScreenViewModel(
            receipt, _wallet, _beat, continueCommand);
    }
}
