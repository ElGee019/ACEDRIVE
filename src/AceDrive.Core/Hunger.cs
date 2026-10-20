// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 26, port of
// Unity GameClockManager.UpdateHunger (the meter gains hungerPerHour on
// every crossed game hour) and YouScreen.OnTrunkItemClicked (eating
// lowers it), fed by the game moment like CityEconomy.
namespace AceDrive.Core;

/// <summary>
/// The appetite: +<see cref="HungerRules.PerHour"/> once per crossed game
/// hour, frozen until onboarding is done (Unity's firstSceneComplete gate) and
/// clamped at <see cref="HungerRules.MaxHunger"/>. Elapsed game minutes arrive
/// from <see cref="GameClock"/>'s moment; only whole hour crossings pay, the
/// remainder carried, so a lagged or a fast clock keeps the exact cadence.
/// Unity fires the pancake-threshold event the one hour the reading lands
/// exactly on the threshold — only while the trunk carries Grandma's
/// Pancakes, and only once ever (hungerEventTriggered is never unset in the
/// source — ported as-is).
/// </summary>
public sealed class Hunger
{
    private const int MinutesPerHour = 60;

    private readonly HungerRules _rules;

    // WHY an anchor flag instead of a default-DateTime sentinel: _since is
    // reassigned after construction, so it cannot be readonly; the flag
    // marks the first AdvanceTo as the "the meter ties to the first game
    // moment it is shown" anchor, so a resumed session never re-pays the
    // hours of earlier ones. Unity owes nothing on a closed process either
    // — a closed process freezes the game clock too.
    private DateTime _since;
    private int _minuteCarry;
    private bool _anchored;

    public Hunger(HungerRules rules, HungerState? saved = null)
    {
        _rules = rules;
        if (saved is { } state)
        {
            // Saved numbers are trusted but clamped: the rules' cap may have
            // changed between sessions, and nothing may resume negative.
            Value = Math.Clamp(state.Hunger, 0, rules.MaxHunger);
            ThresholdAlarmed = state.ThresholdTriggered;
        }
    }

    /// <summary>The meter's reading: 0 (fed) .. MaxHunger (starving).</summary>
    public int Value { get; private set; }

    /// <summary>Unity's maxHunger, the cap the tick clamps against.</summary>
    public int Max => _rules.MaxHunger;

    /// <summary>The job gate boundary (Unity's TOO HUNGRY rule). The overlay
    /// deliberately warns without locking until a food source exists.</summary>
    public bool Blocks => _rules.Blocks(Value);

    /// <summary>Whether the reading sits in Unity's amber label band.</summary>
    public bool IsAmber => HungerRules.IsAmber(Value);

    /// <summary>Whether the one-shot pancake event has fired (Unity
    /// hungerEventTriggered, restored from the save).</summary>
    public bool ThresholdAlarmed { get; private set; }

    /// <summary>
    /// Moves the meter to a game moment: one gain per crossed game hour. The
    /// gate reads the intro like Unity (frozen before it completes, and the
    /// frozen hours never pay out afterwards). Returns the new reading with
    /// its two transitions — most calls answer nothing, which is exactly what
    /// the transition-only logging needs.
    /// </summary>
    public HungerReport AdvanceTo(DateTime gameNow, bool onboarded, bool hasPancakes)
    {
        HungerReport idle = new(Value, ThresholdFired: false, Changed: false);
        if (!_anchored)
        {
            _since = gameNow;
            _anchored = true;
            return idle;
        }
        if (!onboarded)
        {
            // Unity's gate: the appetite only wakes past the intro, so the
            // anchor resets here — no retroactive hours on a later wake-up.
            _since = gameNow;
            _minuteCarry = 0;
            return idle;
        }

        int elapsed = (int)(gameNow - _since).TotalMinutes;
        if (elapsed < 1)
        {
            return idle;
        }
        // Whole minutes only: the leftover is owed to the next call.
        _since = _since.AddMinutes(elapsed);

        _minuteCarry += elapsed;
        int hours = _minuteCarry / MinutesPerHour;
        _minuteCarry -= hours * MinutesPerHour;

        int before = Value;
        bool fired = false;
        if (hours > 0 && Value < _rules.MaxHunger)
        {
            // Unity clamps every tick (Mathf.Min(hunger + inc, maxHunger));
            // summing the hours first gives the same reading because the
            // loader guarantees a positive increment.
            Value = Math.Min(Value + hours * _rules.PerHour, _rules.MaxHunger);
            // Unity's event check runs on gain ticks only: the reading must
            // land exactly on the threshold.
            if (Value == _rules.PancakeThreshold && !ThresholdAlarmed && hasPancakes)
            {
                ThresholdAlarmed = true;
                fired = true;
            }
        }

        return new HungerReport(Value, fired, Value != before);
    }

    // One trunk bite (Unity YouScreen.OnTrunkItemClicked: hunger = max(0,
    // hunger - def.hunger)). The caller owns which items exist and passes
    // only the eaten amount, so this stays free of any item catalogue.
    public void Eat(int reduction)
    {
        if (reduction > 0)
        {
            Value = Math.Max(0, Value - reduction);
        }
    }

    /// <summary>What the next session reopens: the reading plus the one-shot
    /// event's state, copied so the saved view cannot mutate with a later
    /// tick.</summary>
    public HungerState Snapshot() => new(Value, ThresholdAlarmed);
}
