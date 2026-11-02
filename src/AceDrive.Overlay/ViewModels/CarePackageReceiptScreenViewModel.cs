using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The care-package receipt (PORTPLAN item 21): the kits the community centre
/// just took, the stores they were picked up at and the kits left for a later
/// run. The flow has already dropped the kits and banked the ledger by the time
/// this page is built — the receipt only prints the numbers it was handed — and
/// CONTINUE closes it back onto the jobs page.
/// </summary>
public sealed class CarePackageReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly CarePackageReceipt _receipt;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;

    public CarePackageReceiptScreenViewModel(
        CarePackageReceipt receipt, VoiceBeat beat, ICommand continueCommand)
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

    /// <summary>"CARE PACKAGES RECEIPT".</summary>
    public string Title { get; } = Strings.Get("CareReceiptTitle");

    /// <summary>"The neighborhood thanks you for delivering 5 care kits."</summary>
    public string DeliveredLine => Strings.Format("CareReceiptDelivered", _receipt.Delivered);

    /// <summary>True when the run can name the stores it picked up at.</summary>
    public bool HasStores => _receipt.Stores is { Count: > 0 };

    /// <summary>"Picked up at: Max's Grocery Store, Coffee Shop".</summary>
    public string StoresLine => _receipt.Stores is { Count: > 0 } stores
        ? Strings.Format(
            "CareReceiptStores",
            string.Join(Strings.Get("TouristenListSeparator"), stores))
        : string.Empty;

    /// <summary>The kits left for a later run, or the all-done line at zero.</summary>
    public string RemainingLine => _receipt.Remaining > 0
        ? Strings.Format("CareReceiptRemaining", _receipt.Remaining)
        : Strings.Get("CareReceiptAllDelivered");

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
