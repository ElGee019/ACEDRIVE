using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The fuel receipt page (PORTPLAN item 37, port of Unity FuelStationScreen's
/// result block): the litres EVO's pump added, the CR/L that was on the pump,
/// what the bill took, the Free Tank Refill note when a card covered it, and
/// the station attendant's familiarity after the visit. A fill is not a job, so
/// CONTINUE hands back the page the receipt interrupted rather than a menu.
/// </summary>
public sealed class FuelReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly FuelReceipt _receipt;
    private readonly DriverWallet _wallet;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;

    public FuelReceiptScreenViewModel(
        FuelReceipt receipt, DriverWallet wallet,
        VoiceBeat beat, ICommand continueCommand)
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

    /// <summary>The settled data this page renders (tests read it instead of
    /// re-parsing the formatted lines).</summary>
    public FuelReceipt Receipt => _receipt;

    /// <summary>"FUEL RECEIPT".</summary>
    public string Title { get; } = Strings.Get("FuelReceiptTitle");

    /// <summary>"Fuel added: 42.0 L".</summary>
    public string LitresLine => Strings.Format("FuelReceiptLitres", _receipt.Litres);

    /// <summary>"Fuel price: €1.65/L".</summary>
    public string PriceLine => Strings.Format("FuelReceiptPrice", _receipt.PricePerLitre);

    /// <summary>"Paid: €69.30" — the full bill even when it took the balance
    /// negative (the fuel is already in the tank).</summary>
    public string PaidLine => Strings.Format("FuelReceiptPaid", _receipt.Total);

    /// <summary>True when a Free Tank Refill card covered the fill.</summary>
    public bool HasFree => _receipt.Free;

    /// <summary>Why the bill is zero.</summary>
    public string FreeLine { get; } = Strings.Get("FuelReceiptFree");

    /// <summary>Unity familiarity sentence (attendant bands).</summary>
    public bool HasFriendship => _receipt.HasFriendship;

    public string FriendshipLine => _receipt.Friendship is { } friendship
        ? ReceiptText.FriendshipSentence(friendship.OwnerName, friendship.Level)
        : string.Empty;

    /// <summary>Teal highlight when the attendant crossed a tier.</summary>
    public bool FriendshipHighlight => _receipt.Friendship?.TierIncreased == true;

    public string WalletLine => Strings.Format("ReceiptWallet", _wallet.Cash);

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
