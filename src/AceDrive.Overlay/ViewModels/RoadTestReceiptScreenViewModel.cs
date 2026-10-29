using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The road test's receipt page (PORTPLAN item 49, port of Unity
/// RoadTestReceiptScreen): the car that was tested, the shoot's location, the
/// people the issue reached with its formula, the variety of the location
/// against the last shoot, the car journalist's reputation, the magazine price,
/// the commission over the forfeit and the earnings — each with the previous
/// issue's reading, then the personal bests, the wallet after the payment and
/// CONTINUE.
///
/// Every number arrives settled from <see cref="RoadTestReceipt"/>: the page
/// computes nothing and banks nothing, exactly like the other receipt pages, so
/// it can never pay twice.
/// </summary>
public sealed class RoadTestReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly RoadTestReceipt _receipt;
    private readonly DriverWallet _wallet;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;

    public RoadTestReceiptScreenViewModel(
        RoadTestReceipt receipt, DriverWallet wallet, VoiceBeat beat,
        ICommand continueCommand)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(continueCommand);
        _receipt = receipt;
        _wallet = wallet;
        _beat = beat;
        _continueCommand = continueCommand;
        ContinueCommand = new RelayCommand(Continue);
        RebuildVoice();
    }

    /// <summary>The car the test was driven in, all caps like every page.</summary>
    public string CarLine => _receipt.CarName;

    /// <summary>True when the shoot resolved to a door (the line hides otherwise).</summary>
    public bool HasPhotoLocation => _receipt.HasPhotoLocation;

    /// <summary>"Photo location: Street 1 · No. 12" in the shared address shape.</summary>
    public string PhotoLine => Strings.Format(
        "RoadTestReceiptPhoto",
        AddressFormatter.Format(_receipt.PhotoStreet, _receipt.PhotoHouseNumber));

    /// <summary>"120 people from 4000 citizens have bought La Strada."</summary>
    public string PeopleLine => Strings.Format(
        "RoadTestReceiptPeople", _receipt.PeopleReached, _receipt.Citizens);

    /// <summary>Unity's audience formula line under the reading.</summary>
    public string PeopleFormula => Strings.Format(
        "RoadTestReceiptPeopleFormula",
        _receipt.Friends,
        _receipt.Acquaintances,
        _receipt.Friends + _receipt.Acquaintances / 2.0,
        _receipt.Citizens,
        _receipt.PeopleReached);

    /// <summary>"Variety: 62.10% (previously 41.00%)".</summary>
    public string VarietyLine => Strings.Format(
        "RoadTestReceiptVariety", _receipt.Variety * 100.0, _receipt.PreviousVariety * 100.0);

    /// <summary>Unity's variety formula, or the first-photo sentence instead.</summary>
    public string VarietyFormula => _receipt.FirstPhoto
        ? Strings.Get("RoadTestReceiptVarietyFirst")
        : Strings.Format(
            "RoadTestReceiptVarietyFormula",
            _receipt.DistanceKm, _receipt.MaxDistanceKm, _receipt.Variety * 100.0);

    /// <summary>"Car journalist reputation: 65.00 (previously 60.00)".</summary>
    public string JournalistLine => Strings.Format(
        "RoadTestReceiptJournalist",
        _receipt.JournalistReputation, _receipt.PreviousJournalistReputation);

    /// <summary>Unity's car-journalist formula: reputation × result × √variety.</summary>
    public string JournalistFormula => Strings.Format(
        "RoadTestReceiptJournalistFormula",
        _receipt.NaturalReputation, _receipt.ResultReputation, _receipt.JournalistReputation);

    /// <summary>"Magazine price: 32.50 CR (previously 30.00 CR)".</summary>
    public string MagazineLine => Strings.Format(
        "RoadTestReceiptMagazine",
        _receipt.MagazinePrice, _receipt.PreviousMagazinePrice);

    /// <summary>Unity's magazine formula: the reputation halved.</summary>
    public string MagazineFormula => Strings.Format(
        "RoadTestReceiptMagazineFormula",
        _receipt.JournalistReputation, _receipt.MagazinePrice);

    /// <summary>Unity's commission sentence: a tenth of the copies sold.</summary>
    public string CommissionLine => Strings.Format(
        "RoadTestReceiptCommission", _receipt.Commission);

    /// <summary>The reading behind the commission, spelled out like Unity's.</summary>
    public string CommissionNote => Strings.Format(
        "RoadTestReceiptCommissionNote",
        _receipt.CommissionPercent, _receipt.PeopleReached, _receipt.MagazinePrice);

    /// <summary>The flat fee the issue pays whatever the crowd did.</summary>
    public string ForfeitLine => Strings.Format("RoadTestReceiptForfeit", _receipt.Forfeit);

    /// <summary>"Earnings: 512.50 CR (previously 430.00 CR)".</summary>
    public string EarningsLine => Strings.Format(
        "RoadTestReceiptEarnings", _receipt.Earnings, _receipt.PreviousEarnings);

    /// <summary>The wallet after the payment, like every other receipt page.</summary>
    public string CashLine => Strings.Format("RoadTestReceiptCash", _wallet.Cash);

    /// <summary>Unity's "— best —" caption above the personal bests.</summary>
    public string BestHeader { get; } = Strings.Get("RoadTestReceiptBestHeader");

    public string BestPeopleLine => Strings.Format(
        "RoadTestReceiptBestPeople", _receipt.HighestPeopleReached);

    public string BestJournalistLine => Strings.Format(
        "RoadTestReceiptBestJournalist", _receipt.HighestJournalistReputation);

    public string BestMagazineLine => Strings.Format(
        "RoadTestReceiptBestMagazine", _receipt.HighestMagazinePrice);

    public string BestEarningsLine => Strings.Format(
        "RoadTestReceiptBestEarnings", _receipt.HighestEarnings);

    /// <summary>Leaves the visit: the flow hands the control page back.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash
    {
        get => _continueFlash;
        private set => SetField(ref _continueFlash, value);
    }
    private bool _continueFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Continue()
    {
        ContinueFlash = false;
        _continueCommand.Execute(null);
    }

    private void FlashThenContinue()
    {
        ContinueFlash = true;
        _beat.Defer(Continue);
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("ReceiptContinue")] = FlashThenContinue,
        };
    }
}
