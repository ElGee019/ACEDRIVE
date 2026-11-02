using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The thank-you a finished care-package run earns (PORTPLAN item 61, port of
/// Unity's TodoRewardsScreenController): it draws the two rewards the picker
/// offers, applies the one the player picks, banks it with the run's own save
/// and names the outcome on the log.
/// The draw is session state, exactly like Unity's canvas: the run itself was
/// banked at the drop-off, so a restart on the picker page loses the offer
/// rather than re-offering a second reward for the same run.
/// </summary>
public sealed class CarePackageReward
{
    private readonly RewardTable _table;
    private readonly Glovebox _glovebox;
    private readonly Trunk _trunk;
    private readonly DriverWallet _wallet;
    private readonly Random _rng;
    private readonly Action _saveDriver;
    private readonly ILog _log;

    public CarePackageReward(
        RewardTable table, Glovebox glovebox, Trunk trunk, DriverWallet wallet,
        Random rng, Action saveDriver, ILog log)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(glovebox);
        ArgumentNullException.ThrowIfNull(trunk);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(saveDriver);
        ArgumentNullException.ThrowIfNull(log);
        _table = table;
        _glovebox = glovebox;
        _trunk = trunk;
        _wallet = wallet;
        _rng = rng;
        _saveDriver = saveDriver;
        _log = log;
    }

    /// <summary>Cards in the glove compartment right now.</summary>
    public int CardCount => _glovebox.Count(_trunk);

    /// <summary>Slots the glove compartment has.</summary>
    public int Slots => _glovebox.Slots;

    /// <summary>The two rewards this run may pick from, or null when the
    /// catalogue cannot fill a page — the run then closes without one.</summary>
    public RewardOffer? Offer() => RewardDraw.Offer(_table, _rng);

    /// <summary>
    /// Applies the pick and banks it: cash, the glove compartment's cards and its
    /// capacity all ride the driver file, so the claim survives a restart. A
    /// rejected claim (no free slot) saves nothing — nothing moved.
    /// </summary>
    public RewardClaim Claim(RewardCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        RewardClaim claim = _glovebox.Claim(card, _trunk);
        if (!claim.Applied)
        {
            _log.LogInfo(
                $"reward not claimed: the glovebox is full " +
                $"({_glovebox.Count(_trunk)}/{_glovebox.Slots})");
            return claim;
        }

        if (claim.Status == RewardClaimStatus.CashCredited)
        {
            _wallet.AddGift(claim.Cash);
        }

        _saveDriver();
        _log.LogInfo($"reward claimed: {card.Title} ({claim.Status})");
        return claim;
    }
}
