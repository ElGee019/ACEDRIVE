using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The missing-person receipt (ToWhereIsWilly.md §2.7/§3.4, port of Unity
/// WillyReceiptScreen): who was returned, where they were found, the finder's
/// fee with every multiplier that priced it, the certificate and the friendship
/// the run left behind. The flow has already paid and stamped by the time this
/// page is built — the receipt only prints the numbers it was handed — and
/// CONTINUE closes it back onto the control page.
/// </summary>
public sealed class WhereIsWillyReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly WhereIsWillyReceipt _receipt;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;

    public WhereIsWillyReceiptScreenViewModel(
        WhereIsWillyReceipt receipt, VoiceBeat beat, ICommand continueCommand)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(continueCommand);
        _receipt = receipt;
        _beat = beat;
        _continueCommand = continueCommand;
        ContinueCommand = new RelayCommand(Continue);
        RebuildVoice();
    }

    /// <summary>"MISSING PERSON RECEIPT".</summary>
    public string Title { get; } = Strings.Get("WillyReceiptTitle");

    /// <summary>"Returned: Klaus Berger and some colleagues".</summary>
    public string ReturnedLine => Strings.Format("WillyReceiptReturned", _receipt.PersonName);

    /// <summary>"Found at: Klaus's Coffee Shop".</summary>
    public string FoundAtLine => Strings.Format("WillyReceiptFoundAt", _receipt.ShopName);

    /// <summary>"FINDER'S FEE".</summary>
    public string FeeTitle { get; } = Strings.Get("WillyReceiptFeeTitle");

    /// <summary>"Base Fee = 800CR" (Unity prints the base before the multipliers).</summary>
    public string BaseFeeLine => Strings.Format("WillyReceiptBaseFee", _receipt.BaseReward);

    /// <summary>"Coffee shops in town: multiplier x1.25".</summary>
    public string ShopMultiplierLine =>
        Strings.Format("WillyReceiptShopMult", _receipt.ShopMultiplier);

    /// <summary>Unity's two attempt lines: the first try, or the attempt number.</summary>
    public string AttemptLine => _receipt.FirstTry
        ? Strings.Format("WillyReceiptFirstTry", _receipt.AttemptMultiplier)
        : Strings.Format(
            "WillyReceiptAttempt", _receipt.Attempts + 1, _receipt.AttemptMultiplier);

    /// <summary>"REWARD: 1250 CR" — the fee the flow already banked.</summary>
    public string TotalLine => Strings.Format("WillyReceiptTotal", _receipt.FinderFee);

    /// <summary>"Certificate: Bring-Them-Home Certificate Week 3 1924".</summary>
    public string CertificateLine => Strings.Format(
        "WillyReceiptCertificate",
        Strings.Format("WillyCertificateName", _receipt.Week, _receipt.Year));

    /// <summary>Where the certificate went: the trunk, the port's in-car storage.</summary>
    public string CertificateStoredLine { get; } = Strings.Get("WillyReceiptCertificateStored");

    /// <summary>"Friendship level: 2" — printed after the arrival's +1.</summary>
    public string FriendshipLine =>
        Strings.Format("WillyReceiptFriendship", _receipt.FriendshipLevel);

    /// <summary>The page's one way out.</summary>
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
