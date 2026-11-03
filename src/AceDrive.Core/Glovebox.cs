// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// The glove compartment's card rules (PORTPLAN item 61, port of Unity
// RewardCatalog.GetCapacity/CountCards + TodoRewardsScreenController's claim
// guard).
namespace AceDrive.Core;

/// <summary>
/// The glove compartment's capacity and the claim rule over it. The slot count
/// is a driver reading, so it rides <c>DriverWallet.GloveboxSlots</c> (Unity's
/// PlayerData.gloveboxSlots) and is saved with the driver; this class owns only
/// what that count means: which carried stacks are cards, whether one more fits,
/// and what a claim does. Cards are keepsakes in the shared trunk list
/// (<see cref="Trunk.AddKeepsake"/>) — a 0 L stack, so a full car trunk never
/// blocks a reward.
/// </summary>
public sealed class Glovebox
{
    private readonly RewardTable _table;
    private readonly DriverWallet _wallet;

    public Glovebox(RewardTable table, DriverWallet wallet)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(wallet);
        _table = table;
        _wallet = wallet;
        // WHY heal here: a save written before the reward cards existed carries
        // 0 slots, and Unity heals that at the capacity read. Doing it once at the
        // owner keeps every later read a plain number instead of a repair path.
        wallet.EnsureGloveboxSlots(table.DefaultCapacity);
    }

    /// <summary>Cards the glovebox may hold (Unity RewardCatalog.GetCapacity).</summary>
    public int Slots => _wallet.GloveboxSlots;

    /// <summary>Cards carried now, counted off the trunk that stores them.</summary>
    public int Count(Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(trunk);

        int count = 0;
        foreach (TrunkItem item in trunk.Items)
        {
            if (_table.IsCard(item.Name))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>True when one more card fits.</summary>
    public bool HasRoom(Trunk trunk) => Count(trunk) < Slots;

    /// <summary>
    /// Applies one claim. A slot reward raises the capacity, a money reward
    /// answers the CR the caller banks into the saldo (money stays the wallet's
    /// business), and a card lands in the trunk's keepsakes — unless the
    /// glovebox is full, in which case nothing moves so the page can offer the
    /// other reward instead, exactly why Unity keeps both buttons up.
    /// </summary>
    public RewardClaim Claim(RewardCard card, Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(trunk);

        switch (card.Kind)
        {
            case RewardCardKind.GloveboxSlot:
                _wallet.AddGloveboxSlot();
                return new RewardClaim(RewardClaimStatus.SlotAdded, GloveboxSlots: Slots);

            case RewardCardKind.CashBonus:
                return new RewardClaim(
                    RewardClaimStatus.BonusCredited, Bonus: _table.CashBonus, GloveboxSlots: Slots);

            case RewardCardKind.Card:
                if (!HasRoom(trunk))
                {
                    return new RewardClaim(RewardClaimStatus.GloveboxFull, GloveboxSlots: Slots);
                }

                trunk.AddKeepsake(card.Title, 1);
                return new RewardClaim(RewardClaimStatus.Stored, GloveboxSlots: Slots);

            // WHY: a kind added later must not silently behave like a stored
            // card — the claim has to be taught what the new reward does.
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(card), card.Kind, "Unknown reward card kind.");
        }
    }
}
