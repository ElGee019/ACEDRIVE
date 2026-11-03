// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// What a care-package reward hands over on claim (PORTPLAN item 61, port of
// Unity RewardCatalog.CardDefinition's IsSlotReward / IsInstantReward pair).
namespace AceDrive.Core;

/// <summary>What claiming a reward card does.</summary>
public enum RewardCardKind
{
    /// <summary>A card stored in the glove compartment until a later system uses it.</summary>
    Card,

    /// <summary>One more glove compartment slot; consumed on claim, never stored.</summary>
    GloveboxSlot,

    /// <summary>CR credited to the bank saldo on claim; consumed on claim, never stored.</summary>
    CashBonus,
}
