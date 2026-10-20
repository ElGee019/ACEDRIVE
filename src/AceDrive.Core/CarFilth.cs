// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 33, port of
// Unity CarWashService's per-car filth entry (PlayerData.carFilth): an int
// 0..MaxFilth that a completed job raises and a paid wash clears.
namespace AceDrive.Core;

/// <summary>
/// One car's filth reading: 0 (clean) .. <see cref="Max"/> (filthy). Unlike
/// <see cref="Hunger"/> the meter never ticks on the clock — only a completed
/// taxi ride (Unity TaxiJobLifecycleHandler) or delivery run
/// (DeliveryJobHandler) raises it, and only a wash clears it. The reading
/// lives on the owned car and rides garage.json, so a restart reopens the
/// same dirt.
/// </summary>
public sealed class CarFilth
{
    private readonly FilthRules _rules;

    public CarFilth(FilthRules rules, int saved = 0)
    {
        _rules = rules;
        // Saved numbers are trusted but clamped: the rules' cap may have
        // changed between sessions, and nothing may resume negative.
        Value = Math.Clamp(saved, 0, rules.MaxFilth);
    }

    /// <summary>The reading: 0 (clean) .. <see cref="Max"/> (filthy).</summary>
    public int Value { get; private set; }

    /// <summary>Unity's MaxFilth, the cap the accumulation clamps against.</summary>
    public int Max => _rules.MaxFilth;

    /// <summary>Whether this car is too dirty for a taxi pickup (Unity's
    /// filth &gt;= 25 refusal).</summary>
    public bool RefusesPickup => _rules.RefusesPickup(Value);

    /// <summary>Whether the reading sits in Unity's amber HUD band.</summary>
    public bool IsAmber => _rules.IsAmber(Value);

    /// <summary>
    /// One completed job's dirt (Unity IncrementFilth: +1, clamped at
    /// MaxFilth — never time or distance). A non-positive amount is ignored;
    /// at the cap the reading stays put, so a settled job on a filthy car
    /// reports no transition.
    /// </summary>
    public void Add(int amount)
    {
        if (amount > 0)
        {
            Value = Math.Min(Value + amount, _rules.MaxFilth);
        }
    }

    /// <summary>Unity CarWashService.WashCar: a paid wash clears the car.</summary>
    public void Wash() => Value = 0;
}
