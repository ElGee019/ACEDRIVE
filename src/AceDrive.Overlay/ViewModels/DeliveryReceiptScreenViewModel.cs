using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Delivery receipt page (PORTPLAN item 16, port of Unity
/// DeliveryReceiptScreen): the settled run with its total distance, the
/// hub owner's familiarity, the goods money (wholesale out, retail payout
/// in), the tiered distance fare, the totals, the Credibility Points the
/// ramp earned, the XP award with its calculation line, and the XP world
/// standings pair. Skipped until their systems land (Unity hides them too
/// or the port defers them): fines, auto fuel, starter bonus, profit-boost
/// card. Money lines are computed once and never change; the standings
/// block follows the backend's live updates. CONTINUE navigates back, so a
/// voice tap flashes teal first and waits out the shared beat.
/// </summary>
public sealed class DeliveryReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    private readonly DeliveryReceipt _receipt;
    private readonly DriverWallet _wallet;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;
    private readonly StandingsBlock _standings;

    public DeliveryReceiptScreenViewModel(
        DeliveryReceipt receipt, DriverWallet wallet,
        VoiceBeat beat, ICommand continueCommand, IRivalsService rivals)
    {
        _receipt = receipt;
        _wallet = wallet;
        _beat = beat;
        _continueCommand = continueCommand;
        _standings = new StandingsBlock(rivals);

        ContinueCommand = new RelayCommand(Continue);
        TierLines = ReceiptText.TierLines(receipt.TierLines);
        HasFriendship = receipt.HasFriendship;
        FriendshipLine = receipt.HasFriendship
            ? ReceiptText.FriendshipSentence(receipt.HubOwnerName, receipt.FriendshipLevel)
            : string.Empty;
        RebuildVoice();
    }

    public string DistanceLine => Strings.Format("DelReceiptDistance", _receipt.DistanceKm);

    public string WalletLine => Strings.Format("DelReceiptTotalCr", _wallet.Cash);

    public IReadOnlyList<ReceiptLine> TierLines { get; }

    public ReceiptLine PaidLine => new(
        Strings.Get("DelReceiptPaid"), ReceiptText.Amount(_receipt.PaidForGoods));

    public ReceiptLine ReceivedLine => new(
        Strings.Get("DelReceiptReceived"), ReceiptText.Amount(_receipt.ReceivedForGoods));

    public ReceiptLine FareLine => new(
        Strings.Get("DelReceiptFare"), ReceiptText.Amount(_receipt.DistanceCharge));

    public ReceiptLine TotalLine => new(
        Strings.Get("ReceiptTotal"), ReceiptText.Amount(_receipt.TotalEarned));

    public string XpLine => Strings.Format("ReceiptXp", _receipt.XpEarned);

    /// <summary>Delivery's calculation line: no car class or rarity, the
    /// ride hauls goods (Unity DeliveryReceiptScreen.PopulateXp).</summary>
    public string XpCalcLine => Strings.Format(
        "DelReceiptXpCalc", _receipt.DistanceKm, _receipt.StreetKnowledgePct);

    public string TotalXpLine => Strings.Format("ReceiptTotalXp", _wallet.Xp);

    /// <summary>Unity's CredP pair: the ramp's earn for this run and the
    /// career's total after it. Hidden when no owned hub sourced the run.</summary>
    public bool HasCredP => _receipt.HasCredP;

    public string CredPEarnedLine => Strings.Format("DelReceiptCredPEarned", _receipt.CredPEarned);

    public string TotalCredPLine => Strings.Format("DelReceiptCredPTotal", _receipt.TotalCredP);

    /// <summary>The page's XP world standings block: rank line, delta line
    /// and pulse flag, live on the board's event (StandingsBlock owns it).</summary>
    public StandingsBlock Standings => _standings;

    /// <summary>False when the run rested on no owned hub.</summary>
    public bool HasFriendship { get; }

    /// <summary>Teal highlight when the hub owner crossed a familiarity tier.</summary>
    public bool FriendshipHighlight => _receipt.FriendshipTierIncreased;

    /// <summary>Hub-owner familiarity sentence, empty when there is no owner.</summary>
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
