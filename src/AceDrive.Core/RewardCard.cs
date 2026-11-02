// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// One reward the community centre offers after a care-package run (PORTPLAN
// item 61, port of Unity RewardCatalog.CardDefinition).
namespace AceDrive.Core;

/// <summary>
/// One care-package reward: the title the glove compartment stores, the
/// function text the picker prints, and what claiming it does. The title is the
/// card's identity — a carried card is matched by it across sessions — so it is
/// content authored in Data/reward_cards.json, not a UI label.
/// </summary>
public sealed record RewardCard(
    string Title,
    string Description,
    RewardCardKind Kind = RewardCardKind.Card)
{
    /// <summary>True when claiming stores a card in the glove compartment.</summary>
    public bool IsCard => Kind == RewardCardKind.Card;
}
