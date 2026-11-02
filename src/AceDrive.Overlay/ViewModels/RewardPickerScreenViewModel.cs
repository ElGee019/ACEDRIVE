using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The care-package reward picker (PORTPLAN item 61, port of Unity's
/// TodoRewardsScreen/CANVASREWARDS): the two rewards the community centre offers
/// for a finished run, one click to claim, and CONTINUE back to the jobs page.
/// The page prints what the claim did — a card in the glove compartment, a slot
/// more room, the cash bonus — and keeps both rewards up when the glovebox is
/// full, so the player can take the other one instead (Unity's full-text branch).
/// Once a claim lands the buttons go away and only CONTINUE is left, exactly
/// like Unity's SetButtonsVisible(false).
/// </summary>
public sealed class RewardPickerScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly CarePackageReward _rewards;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;

    public RewardPickerScreenViewModel(
        RewardOffer offer, CarePackageReward rewards, VoiceBeat beat, ICommand continueCommand)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(continueCommand);
        _rewards = rewards;
        _beat = beat;
        _continueCommand = continueCommand;

        var rows = new List<RewardCardOption>(2);
        foreach (RewardCard card in offer.Cards)
        {
            rows.Add(new RewardCardOption(card));
        }
        Cards = rows;

        ClaimCommand = new ParameterizedCommand<RewardCardOption>(Claim);
        ContinueCommand = new RelayCommand(Continue);
        RebuildVoice();
    }

    /// <summary>"CARE PACKAGE REWARDS".</summary>
    public string Title { get; } = Strings.Get("RewardPickerTitle");

    /// <summary>"The community centre thanks you. Choose one reward."</summary>
    public string HintLine { get; } = Strings.Get("RewardPickerHint");

    /// <summary>The two rewards on offer, in page order.</summary>
    public IReadOnlyList<RewardCardOption> Cards { get; }

    /// <summary>The left card (the catalogue's first pick).</summary>
    public RewardCardOption First => Cards[0];

    /// <summary>The right card (the catalogue's second pick).</summary>
    public RewardCardOption Second => Cards[1];

    /// <summary>One card button; the parameter is the <see cref="RewardCardOption"/> tapped.</summary>
    public ICommand ClaimCommand { get; }

    /// <summary>True while a reward can still be claimed (Unity hides both
    /// buttons once one landed).</summary>
    public bool OfferOpen
    {
        get => _offerOpen;
        private set => SetField(ref _offerOpen, value);
    }
    private bool _offerOpen = true;

    /// <summary>"Glovebox cards: 1/2".</summary>
    public string GloveboxLine =>
        Strings.Format("RewardPickerGlovebox", _rewards.CardCount, _rewards.Slots);

    /// <summary>What the claim did, or the full-glovebox warning.</summary>
    public string Notice
    {
        get => _notice;
        private set
        {
            if (SetField(ref _notice, value))
            {
                OnPropertyChanged(nameof(HasNotice));
            }
        }
    }
    private string _notice = string.Empty;

    /// <summary>True when there is a line under the cards.</summary>
    public bool HasNotice => Notice.Length > 0;

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

    // Unity OnRewardButtonClicked: a slot reward is consumed into the capacity, a
    // cash reward is banked and a card reward is stored — unless the glovebox is
    // full, which leaves every reward up so the other one can be taken instead.
    private void Claim(RewardCardOption? option)
    {
        if (option is null || !OfferOpen)
        {
            return;
        }

        RewardClaim claim = _rewards.Claim(option.Card);
        if (!claim.Applied)
        {
            Notice = Strings.Get("RewardPickerFull");
            return;
        }

        OfferOpen = false;
        Notice = claim.Status switch
        {
            RewardClaimStatus.SlotAdded => Strings.Format("RewardPickerSlotAdded", claim.GloveboxSlots),
            RewardClaimStatus.CashCredited => Strings.Format("RewardPickerCash", claim.Cash),
            _ => Strings.Format("RewardPickerStored", option.Title),
        };
        // The capacity the line reads moved with the claim.
        OnPropertyChanged(nameof(GloveboxLine));
        // A claimed page can only hear CONTINUE: the rewards are gone.
        RebuildVoice();
    }

    private void Continue()
    {
        ContinueFlash = false;
        _continueCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>();
        if (OfferOpen)
        {
            // Each reward answers its own title: the two buttons share no label,
            // so a spoken "double next tip" can name the pick it means.
            foreach (RewardCardOption option in Cards)
            {
                RewardCardOption hit = option;
                actions[hit.Title] = () => Claim(hit);
            }
        }

        // CONTINUE navigates: glow first, act at the end of the shared beat.
        actions[Strings.Get("RewardPickerContinue")] = () =>
        {
            ContinueFlash = true;
            _beat.Defer(Continue);
        };
        VoiceActions = actions;
    }
}
