// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md items 11/13/61.
namespace AceDrive.Core;

/// <summary>One procedurally drawn passenger identity: the parts stay data,
/// <see cref="Full"/> mirrors Unity's finalCustomerName composition
/// (base name + companion suffix). <see cref="ForCapacity"/> drops the suffix
/// group for a car that seats a single customer.</summary>
public sealed record ProceduralName(string FirstName = "", string Surname = "", string Suffix = "")
{
    /// <summary>The resident's name without the companion suffix: Unity's
    /// customerBaseName, and the key familiarity and the resident table use.</summary>
    public string BaseName => $"{FirstName} {Surname}".Trim();

    /// <summary>The spoken identity, "Klaus Berger and some colleagues".</summary>
    public string Full => Suffix.Length == 0 ? BaseName : $"{BaseName} {Suffix}";

    /// <summary>The identity this ride carries in a car with room for
    /// <paramref name="capacity"/> passengers. A single seat means exactly one
    /// customer, so the companion group ("and some friends") would name people
    /// with nowhere to sit and is dropped. The resident keeps their full
    /// drawn identity for the next ride in a bigger car.</summary>
    public ProceduralName ForCapacity(int capacity) =>
        capacity > 1 ? this : this with { Suffix = "" };
}
