// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// What a carried reward card does at the moment its source event fires
// (PORTPLAN items 42/61, port of Unity's per-card helper classes:
// HeatFreeRides, InvincibleRides, NextFineWaived, HalfDamageReduction,
// DRProtection, DeliveryInsurance, DeliveryProfitBoost, DoubleNextTip,
// AccidentLoyalty, WorkerBonus).
namespace AceDrive.Core;

/// <summary>
/// The effect a stored, single-use reward card applies when its event fires,
/// and the identity the card is consumed by. Data, not behaviour: the flow
/// that owns the event asks the glovebox for the effect, applies the amount
/// the catalogue authored and consumes one card.
/// WHY the card title is not the switch: a title is a saved identity, so a
/// rename must not silently turn an effect off; the enum is the contract, and
/// Data/reward_cards.json declares which card carries which effect.
/// Three effects have no source in the port yet and stay carried until their
/// systems land: <see cref="FreeTankRefill"/> (fuel sim declined, PORTPLAN 37),
/// <see cref="WorkerBonus"/> (employee deliveries unbuilt, item 23) and
/// <see cref="CustomerForgiveness"/> (taxi wrong-address cancellation unbuilt,
/// item 11).
/// </summary>
public enum RewardCardEffect
{
    /// <summary>No effect: a slot or cash reward that is consumed on claim.</summary>
    None,

    /// <summary>One free tank refill (dormant: no fuel sim).</summary>
    FreeTankRefill,

    /// <summary>One repair bill covered in full (the accident body repair).</summary>
    FreeCarRepair,

    /// <summary>No friendship loss after a wrong-address cancellation (dormant).</summary>
    CustomerForgiveness,

    /// <summary>Refund the wholesale CR invested in a delivery that fails.</summary>
    DeliveryInsurance,

    /// <summary>No friendship loss after an accident that ends a taxi ride or delivery run.</summary>
    AccidentLoyalty,

    /// <summary>Halve the accident repair bill before insurance (Amount is the factor).</summary>
    HalfDamage,

    /// <summary>One more attempt at the road test leg just driven.</summary>
    JournalistRetake,

    /// <summary>Scale the delivery's goods earnings (Amount is the multiplier).</summary>
    DeliveryProfitBoost,

    /// <summary>Scale the customer tip (Amount is the multiplier; 2 doubles it).</summary>
    DoubleNextTip,

    /// <summary>Waive the next speed-challenge fine and keep its tip.</summary>
    FineWaived,

    /// <summary>No police patrols while rides remain (ticked per completed job).</summary>
    HeatFreeRides,

    /// <summary>No accidents while rides remain (ticked per completed job).</summary>
    InvincibleRides,

    /// <summary>Scale an employee delivery's return (dormant: no employee jobs).</summary>
    WorkerBonus,

    /// <summary>Keep the driver-rating skills intact through an accident.</summary>
    DriverRatingProtection,
}
