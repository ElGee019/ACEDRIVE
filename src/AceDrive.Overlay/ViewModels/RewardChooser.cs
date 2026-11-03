using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The two-reward choice a finished care-package run offers (PORTPLAN item 61,
/// port of Unity's TodoRewardsScreen buttons): the drawn pair, the one claim and
/// the outcome line the page prints. It is a section the delivered receipt
/// composes, not a page of its own — the page owns the commands, the navigation
/// and the voice map, this owns the claim's rules. A full glovebox refuses the
/// card and leaves both rewards up so the other pick can be taken, exactly like
/// Unity's full-text branch.
/// </summary>
public sealed class RewardChooser
{
    private readonly CarePackageReward _rewards;

    public RewardChooser(RewardOffer offer, CarePackageReward rewards)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(rewards);
        _rewards = rewards;

        var rows = new List<RewardCardOption>(2);
        foreach (RewardCard card in offer.Cards)
        {
            rows.Add(new RewardCardOption(card));
        }
        Cards = rows;
    }

    /// <summary>The two rewards on offer, left to right.</summary>
    public IReadOnlyList<RewardCardOption> Cards { get; }

    /// <summary>True while a reward can still be claimed (Unity hides both
    /// cards once one landed).</summary>
    public bool OfferOpen { get; private set; } = true;

    /// <summary>What the claim did, or the full-glovebox warning.</summary>
    public string Notice { get; private set; } = string.Empty;

    /// <summary>True when there is a line under the cards.</summary>
    public bool HasNotice => Notice.Length > 0;

    /// <summary>"Glove compartment cards: 1/2".</summary>
    public string GloveboxLine =>
        Strings.Format("RewardPickerGlovebox", _rewards.CardCount, _rewards.Slots);

    /// <summary>
    /// Applies the pick. False when the glovebox refused a card, which keeps
    /// both rewards up; a run pays one reward, so a second claim is ignored.
    /// </summary>
    public bool Claim(RewardCardOption option)
    {
        ArgumentNullException.ThrowIfNull(option);
        if (!OfferOpen)
        {
            return false;
        }

        RewardClaim claim = _rewards.Claim(option.Card);
        if (!claim.Applied)
        {
            Notice = Strings.Get("RewardPickerFull");
            return false;
        }

        OfferOpen = false;
        Notice = claim.Status switch
        {
            RewardClaimStatus.SlotAdded => Strings.Format("RewardPickerSlotAdded", claim.GloveboxSlots),
            RewardClaimStatus.BonusCredited => Strings.Format("RewardPickerBank", claim.Bonus),
            _ => Strings.Format("RewardPickerStored", option.Title),
        };
        return true;
    }
}
