using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The care-package delivered page (PORTPLAN items 21 and 61, Unity's completion
/// edge: the receipt numbers and the CANVASREWARDS pair on one page). It prints
/// the kits the community centre just took, the stores they were picked up at,
/// the kits left for a later run, and presents the run's two reward cards to
/// choose one from. The flow has already dropped the kits and banked the ledger
/// by the time this page is built; the claim is the page's own, and CONTINUE
/// closes it back onto the jobs page.
/// </summary>
public sealed class CarePackageReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly CarePackageReceipt _receipt;
    private readonly RewardChooser? _reward;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;

    public CarePackageReceiptScreenViewModel(
        CarePackageReceipt receipt, RewardChooser? reward, VoiceBeat beat, ICommand continueCommand)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(continueCommand);
        _receipt = receipt;
        _reward = reward;
        _beat = beat;
        _continueCommand = continueCommand;
        RewardHint = reward is null ? string.Empty : Strings.Get("RewardPickerHint");
        ClaimCommand = new ParameterizedCommand<RewardCardOption>(Claim);
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

    /// <summary>True when the run drew a reward pair (a catalogue too thin to
    /// fill one closes the page without cards).</summary>
    public bool HasReward => _reward is not null;

    /// <summary>True while both reward cards are still up for grabs.</summary>
    public bool RewardOpen => _reward?.OfferOpen == true;

    /// <summary>The two cards, left to right; empty when no pair was drawn.</summary>
    public IReadOnlyList<RewardCardOption> RewardCards => _reward?.Cards ?? [];

    /// <summary>"The community centre thanks you for the run. Choose one reward."</summary>
    public string RewardHint { get; }

    /// <summary>What the claim did, or the full-glovebox warning.</summary>
    public string RewardNotice => _reward?.Notice ?? string.Empty;

    /// <summary>True when there is an outcome line under the cards.</summary>
    public bool HasRewardNotice => _reward?.HasNotice == true;

    /// <summary>"Glove compartment cards: 1/2".</summary>
    public string GloveboxLine => _reward?.GloveboxLine ?? string.Empty;

    /// <summary>One card button; the parameter is the <see cref="RewardCardOption"/> tapped.</summary>
    public ICommand ClaimCommand { get; }

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

    // Unity OnRewardButtonClicked: the chooser settles the claim (card, slot or
    // cash) and either closes the offer or reports the full glovebox; either way
    // the page re-reads the section it prints.
    private void Claim(RewardCardOption? option)
    {
        if (_reward is null || option is null)
        {
            return;
        }

        _reward.Claim(option);
        OnPropertyChanged(nameof(RewardOpen));
        OnPropertyChanged(nameof(RewardNotice));
        OnPropertyChanged(nameof(HasRewardNotice));
        OnPropertyChanged(nameof(GloveboxLine));
        // A claimed page can only hear CONTINUE: the rewards are gone.
        RebuildVoice();
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
        var actions = new Dictionary<string, Action>();
        if (_reward is { OfferOpen: true })
        {
            // Each reward answers its own title: the two cards share no label,
            // so a spoken "double next tip" can name the pick it means.
            foreach (RewardCardOption option in _reward.Cards)
            {
                RewardCardOption hit = option;
                actions[hit.Title] = () => Claim(hit);
            }
        }

        // CONTINUE navigates: glow first, act at the end of the shared beat.
        actions[Strings.Get("ReceiptContinue")] = FlashThenContinue;
        VoiceActions = actions;
    }
}
