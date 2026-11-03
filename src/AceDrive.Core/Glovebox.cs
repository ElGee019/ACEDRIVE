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
/// what a claim does, how a counter-limited card wears down on a completed
/// job, and which card an event consumes. Cards are keepsakes in the shared
/// trunk list (<see cref="Trunk.AddKeepsake"/>) — a 0 L stack, so a full car
/// trunk never blocks a reward.
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
    /// The catalogue card behind <paramref name="effect"/>, or null when the
    /// loaded catalogue does not define it. A flow reads its scaling amount
    /// from here, so a balance change is a data edit, not a code edit.
    /// </summary>
    public RewardCard? CardFor(RewardCardEffect effect) => _table.Find(effect);

    /// <summary>
    /// The catalogue row behind a carried stack's name, or null when the name
    /// is not a reward card (a certificate, an award, a food item). The trunk
    /// page reads it to print the card's function text beside its title.
    /// </summary>
    public RewardCard? Definition(string? name) => _table.Find(name);

    /// <summary>
    /// True while at least one card carrying <paramref name="effect"/> is
    /// carried (a one-shot card waiting for its event, or a counter card with
    /// rides left).
    /// </summary>
    public bool Carries(RewardCardEffect effect, Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(trunk);

        foreach (TrunkItem item in trunk.Items)
        {
            if (_table.Find(item.Name) is { } card && card.Effect == effect)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True while a counter card with charges left carries
    /// <paramref name="effect"/>: the "next 5 rides" pair is live for the ride
    /// that starts now. A spent stack is already dropped by the trunk.
    /// </summary>
    public bool HasCharges(RewardCardEffect effect, Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(trunk);

        foreach (TrunkItem item in trunk.Items)
        {
            if (item.UsesLeft > 0
                && _table.Find(item.Name) is { } card
                && card.Effect == effect)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Spends exactly one card carrying <paramref name="effect"/>, the way
    /// Unity's per-effect helper removes one glovebox card. Stacked single-use
    /// cards cover consecutive events one at a time; false when none is
    /// carried, so the caller applies the undiscounted outcome.
    /// </summary>
    public bool TryConsume(RewardCardEffect effect, Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(trunk);

        for (int i = trunk.Items.Count - 1; i >= 0; i--)
        {
            TrunkItem item = trunk.Items[i];
            if (_table.Find(item.Name) is { } card
                && card.Effect == effect
                && trunk.RemoveOne(item.Name))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Heals the charge count of a counter card saved before the counters
    /// existed (or before the card was authored with a limit). The limit is
    /// content, so the catalogue supplies it; a card already carrying a limit
    /// keeps its count, which is why replaying this at boot cannot refill one.
    /// </summary>
    public void HealCardUses(Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(trunk);

        foreach (TrunkItem item in trunk.Items)
        {
            if (_table.Find(item.Name) is { HasCounter: true } card)
            {
                _ = trunk.HealCardUses(item.Name, card.Uses);
            }
        }
    }

    /// <summary>
    /// Spends one charge of every carried counter card on a completed job
    /// (Unity's "next 5 rides" cards tick per ride). Returns how many cards
    /// ticked, so the settle can log the wear; a card with no charges left is
    /// dropped from the compartment by <see cref="Trunk.ConsumeUse"/>.
    /// </summary>
    public int RegisterCompletedJob(Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(trunk);

        int ticked = 0;
        // Backwards: ConsumeUse removes a spent stack, so a forward loop would
        // skip the next card when one disappears under it.
        for (int i = trunk.Items.Count - 1; i >= 0; i--)
        {
            if (_table.IsCard(trunk.Items[i].Name) && trunk.ConsumeUse(trunk.Items[i].Name))
            {
                ticked++;
            }
        }

        return ticked;
    }

    /// <summary>
    /// Applies one claim. A slot reward raises the capacity, a money reward
    /// answers the CR the caller banks into the saldo (money stays the wallet's
    /// business), and a card lands in the trunk's keepsakes — unless the
    /// glovebox is full, in which case nothing moves so the page can offer the
    /// other reward instead, exactly why Unity keeps both buttons up. A card
    /// with a use limit starts on its full count.
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

                if (card.HasCounter)
                {
                    trunk.AddCard(card.Title, card.Uses);
                }
                else
                {
                    trunk.AddKeepsake(card.Title, 1);
                }
                return new RewardClaim(RewardClaimStatus.Stored, GloveboxSlots: Slots);

            // WHY: a kind added later must not silently behave like a stored
            // card — the claim has to be taught what the new reward does.
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(card), card.Kind, "Unknown reward card kind.");
        }
    }
}
