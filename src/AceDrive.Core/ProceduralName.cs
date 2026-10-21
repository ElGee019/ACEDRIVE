// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/13/61.
namespace AceDrive.Core;

/// <summary>One procedurally drawn passenger identity: the parts stay data,
/// <see cref="Full"/> mirrors Unity's finalCustomerName composition
/// (base name + companion suffix). A single-passenger car drops the suffix
/// group; that rule waits for per-car passenger capacity in ACE.</summary>
public sealed record ProceduralName(string FirstName = "", string Surname = "", string Suffix = "")
{
    /// <summary>The resident's name without the companion suffix: Unity's
    /// customerBaseName, and the key familiarity and the resident table use.</summary>
    public string BaseName => $"{FirstName} {Surname}".Trim();

    /// <summary>The spoken identity, "Klaus Berger and some colleagues".</summary>
    public string Full => Suffix.Length == 0 ? BaseName : $"{BaseName} {Suffix}";
}
