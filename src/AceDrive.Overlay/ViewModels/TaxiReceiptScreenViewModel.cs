using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Taxi receipt page (PORTPLAN item 11, port of Unity TaxiReceiptScreen):
/// the settled dropoff with its per-tier fare breakdown, forfait, tip and
/// fine, the total earned, the wallet and shift totals, and the XP award
/// with its calculation line. Built fresh per dropoff, so every line is
/// computed once in the constructor and never changes. CONTINUE navigates
/// back, so a voice tap flashes teal first and waits out the shared beat
/// (proposal-page precedent).
/// </summary>
public sealed class TaxiReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    private readonly TaxiReceipt _receipt;
    private readonly DriverWallet _wallet;
    private readonly TaxiShift _shift;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;

    public TaxiReceiptScreenViewModel(
        TaxiReceipt receipt, string customerName, DriverWallet wallet, TaxiShift shift,
        VoiceBeat beat, ICommand continueCommand)
    {
        _receipt = receipt;
        _wallet = wallet;
        _shift = shift;
        _beat = beat;
        _continueCommand = continueCommand;

        ContinueCommand = new RelayCommand(Continue);
        CustomerLine = Strings.Format("ReceiptCustomer", receipt.Capacity, customerName);
        TierLines = BuildTierLines(receipt);
        if (receipt.HasFriendship)
        {
            string first = FriendshipLevels.DisplayFirstName(receipt.FriendshipName);
            FriendshipLine = Strings.Format(FriendshipKey(receipt.FriendshipLevel), first, receipt.FriendshipLevel);
        }
        else
        {
            FriendshipLine = string.Empty;
        }
        RebuildVoice();
    }

    public string DistanceLine => Strings.Format("ReceiptDistance", _receipt.DistanceKm);

    public string CustomerLine { get; }

    public IReadOnlyList<string> TierLines { get; }

    public string FareLine => Strings.Format(
        "ReceiptFare", _receipt.Capacity,
        _receipt.DistanceCharge / _receipt.Capacity, _receipt.DistanceCharge);

    public string ForfaitLine => Strings.Format("ReceiptForfait", _receipt.Forfait);

    public bool HasTip => _receipt.Tip > 0.0;
    public string TipLine => Strings.Format("ReceiptTip", _receipt.Tip);

    public bool HasFine => _receipt.FineDeduction > 0.0;
    public string FineLine => Strings.Format("ReceiptFine", _receipt.FineDeduction);

    public string TotalLine => Strings.Format("ReceiptTotal", _receipt.Total);

    public string WalletLine => Strings.Format("ReceiptWallet", _wallet.Cash);

    public string ShiftLine => Strings.Format("ReceiptShift", _shift.JobsDone, _shift.Earned);

    public string XpLine => Strings.Format("ReceiptXp", _receipt.XpEarned);

    public string XpCalcLine => Strings.Format(
        "ReceiptXpCalc", _receipt.DistanceKm, _receipt.CarClass,
        _receipt.CarRarity, _receipt.StreetKnowledgePct);

    public string TotalXpLine => Strings.Format("ReceiptTotalXp", _wallet.Xp);

    /// <summary>False when the passenger matched no known resident.</summary>
    public bool HasFriendship => _receipt.HasFriendship;

    /// <summary>Teal highlight when the ride crossed a familiarity tier.</summary>
    public bool FriendshipHighlight => _receipt.FriendshipTierIncreased;

    /// <summary>Unity familiarity sentence, empty when there is no resident.</summary>
    public string FriendshipLine { get; }

    public ICommand ContinueCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash { get => _continueFlash; private set => SetField(ref _continueFlash, value); }
    private bool _continueFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // Tier wording lives in resx (one key per band) so translators can
    // reorder placeholders without touching code.
    private static string FriendshipKey(int level) => FriendshipLevels.GetTier(level) switch
    {
        0 => "FriendshipStranger",
        1 => "FriendshipAcquaintance",
        2 => "FriendshipFriend",
        3 => "FriendshipGood",
        4 => "FriendshipClose",
        _ => "FriendshipBff",
    };

    private static List<string> BuildTierLines(TaxiReceipt receipt)
    {
        List<string> lines = [];
        foreach (TaxiReceiptTier tier in receipt.TierLines)
        {
            lines.Add(tier.IsOpen
                ? Strings.Format(
                    "ReceiptTierOpen", tier.StartKm, tier.RatePerKm, tier.Km, tier.Charge)
                : Strings.Format(
                    "ReceiptTier", tier.StartKm, tier.StartKm + tier.WidthKm,
                    tier.RatePerKm, tier.Km, tier.Charge));
        }
        return lines;
    }

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
