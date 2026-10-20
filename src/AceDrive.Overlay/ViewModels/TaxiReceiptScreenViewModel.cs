using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Taxi receipt page (PORTPLAN item 11, port of Unity TaxiReceiptScreen):
/// the settled dropoff with its per-tier fare breakdown, forfait, brand
/// sponsorship and fine, the total earned, the wallet and shift totals,
/// the XP award with its calculation line, and the XP world standings
/// pair. Money lines are computed once in the constructor and never
/// change; the standings block follows the backend's live updates, exactly
/// like Unity's rivals block. CONTINUE navigates back, so a voice tap
/// flashes teal first and waits out the shared beat (proposal-page
/// precedent).
/// </summary>
public sealed class TaxiReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    private readonly TaxiReceipt _receipt;
    private readonly DriverWallet _wallet;
    private readonly TaxiShift _shift;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;
    private readonly StandingsBlock _standings;
    public TaxiReceiptScreenViewModel(
        TaxiReceipt receipt, string customerName, DriverWallet wallet, TaxiShift shift,
        VoiceBeat beat, ICommand continueCommand, IRivalsService rivals)
    {
        _receipt = receipt;
        _wallet = wallet;
        _shift = shift;
        _beat = beat;
        _continueCommand = continueCommand;
        _standings = new StandingsBlock(rivals);

        ContinueCommand = new RelayCommand(Continue);
        CustomerLine = Strings.Format("ReceiptCustomer", receipt.Capacity, customerName);
        TierLines = ReceiptText.TierLines(receipt.TierLines);
        HasFriendship = receipt.HasFriendship;
        if (receipt.HasFriendship)
        {
            FriendshipLine = ReceiptText.FriendshipSentence(receipt.FriendshipName, receipt.FriendshipLevel);
        }
        else
        {
            FriendshipLine = string.Empty;
        }

        RebuildVoice();
    }

    public string DistanceLine => Strings.Format("ReceiptDistance", _receipt.DistanceKm);

    public string CustomerLine { get; }

    public IReadOnlyList<ReceiptLine> TierLines { get; }

    public ReceiptLine FareLine => new(
        Strings.Format("ReceiptFare", _receipt.Capacity,
            _receipt.DistanceCharge / _receipt.Capacity),
        ReceiptText.Amount(_receipt.DistanceCharge));

    public ReceiptLine ForfaitLine => new(
        Strings.Get("ReceiptForfait"), ReceiptText.Amount(_receipt.Forfait));

    public bool HasBrandBonus => _receipt.HasBrandBonus;

    /// <summary>Unity's sponsorship row, hidden like tip/fine when zero.</summary>
    public ReceiptLine SponsorshipLine => new(
        Strings.Format("ReceiptSponsorship", _receipt.BrandName), ReceiptText.Amount(_receipt.BrandBonus));

    /// <summary>Unity's rate explanation (pct + lifetime brand km).</summary>
    public string BrandBonusLine => Strings.Format(
        "ReceiptBrandBonus", _receipt.BrandName,
        _receipt.BrandBonusRate * 100.0, _receipt.BrandDistanceKm);

    public bool HasTip => _receipt.Tip > 0.0;
    public ReceiptLine TipLine => new(Strings.Get("ReceiptTip"), ReceiptText.Amount(_receipt.Tip));

    public bool HasFine => _receipt.FineDeduction > 0.0;

    /// <summary>No fine means no mention: the row stays in the layout as an
    /// empty line (the view hides without collapsing), never a zero fine.</summary>
    public ReceiptLine FineLine => HasFine
        ? new(Strings.Get("ReceiptFine"), Strings.Format("ReceiptFineAmount", _receipt.FineDeduction))
        : new(string.Empty, string.Empty);

    public ReceiptLine TotalLine => new(Strings.Get("ReceiptTotal"), ReceiptText.Amount(_receipt.Total));

    public string WalletLine => Strings.Format("ReceiptWallet", _wallet.Cash);

    public string ShiftLine => Strings.Format("ReceiptShift", _shift.JobsDone, _shift.Earned);

    public string XpLine => Strings.Format("ReceiptXp", _receipt.XpEarned);

    public string XpCalcLine => Strings.Format(
        "ReceiptXpCalc", _receipt.DistanceKm, _receipt.CarClass,
        _receipt.CarRarity, _receipt.StreetKnowledgePct);

    public string TotalXpLine => Strings.Format("ReceiptTotalXp", _wallet.Xp);

    /// <summary>The page's XP world standings block: rank line, delta line
    /// and pulse flag, live on the board's event (StandingsBlock owns it).</summary>
    public StandingsBlock Standings => _standings;

    /// <summary>False when the passenger matched no known resident.</summary>
    public bool HasFriendship { get; }

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

    private void Continue()
    {
        _standings.Unhook();
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
