// Balance table for the appetite (PORTPLAN item 26, port of Unity
// GameTimeConfig.asset's hunger block + PlayerDefaults.maxHunger): the
// numbers the meter ticks against, one place to edit. The job gate lives
// here too — Unity MapScreen refuses the job centre at hunger >= maxHunger
// minus one ("TOO HUNGRY" popup page, ported as <see cref="Blocks"/>).
namespace AceDrive.Core;

public sealed record HungerRules(int MaxHunger, int PerHour, int PancakeThreshold)
{
    /// <summary>Unity's shipped GameTimeConfig.asset (hungerPerHour 1,
    /// hungerPancakeThreshold 14) with PlayerDefaults' maxHunger 36. Fallback
    /// for tests and for a missing or invalid Data/hunger_rules.json — the
    /// loaded file is the source of truth.</summary>
    public static readonly HungerRules Default = new(
        MaxHunger: 36,
        PerHour: 1,
        PancakeThreshold: 14);

    /// <summary>True once another job is refused (Unity MapScreen's block
    /// check on the job-centre tap: hunger &gt;= maxHunger - 1).</summary>
    public bool Blocks(int hunger) => hunger >= MaxHunger - 1;

    // Amber band of the hunger label, ported from Unity YouScreen's trunk
    // colouring: h >= 16 turns the reading orange until h < 30. Bands are
    // display data (YouScreen), not economy balance — no ctor parameter, no
    // JSON round-trip. The >= 30 branch in Unity picks the same calm colour
    // as below 16, so the observable rule is exactly this one interval.
    public const int AmberFrom = 16;
    public const int AmberUntil = 30;

    /// <summary>Whether a reading wears Unity's amber band colour. Static:
    /// the band compares against fixed hour edges, not the rules' instance
    /// numbers (CA1822).</summary>
    public static bool IsAmber(int hunger) => hunger >= AmberFrom && hunger < AmberUntil;
}
