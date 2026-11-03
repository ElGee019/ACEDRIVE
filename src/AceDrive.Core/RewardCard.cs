// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// One reward the community centre offers after a care-package run (PORTPLAN
// item 61, port of Unity RewardCatalog.CardDefinition).
namespace AceDrive.Core;

/// <summary>
/// One care-package reward: the title the glove compartment stores, the
/// function text the delivered page prints, and what claiming it does. The title
/// is the card's identity — a carried card is matched by it across sessions — so
/// it is content authored in Data/reward_cards.json, not a UI label.
/// </summary>
public sealed record RewardCard(
    string Title,
    string Description,
    RewardCardKind Kind = RewardCardKind.Card,
    RewardCardEffect Effect = RewardCardEffect.None,
    double Amount = 0.0,
    int Uses = 0)
{
    /// <summary>True when claiming stores a card in the glove compartment.</summary>
    public bool IsCard => Kind == RewardCardKind.Card;

    /// <summary>
    /// True when the card applies an effect at a game event. A stored card
    /// without one would be a keepsake that never fires, so the catalogue
    /// validation rejects it.
    /// </summary>
    public bool HasEffect => Effect != RewardCardEffect.None;

    /// <summary>
    /// True when the card carries a countdown of jobs ("the next 5 rides").
    /// 0 uses is a card without a counter: its owner consumes it on its own
    /// event, so a completed job must not tick it.
    /// </summary>
    public bool HasCounter => Uses > 0;
}
