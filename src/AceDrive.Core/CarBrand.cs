// Car-id -> spoken wake brand, pure functions only (no file I/O).
// Spec source: companion carwatch.py, ported including the log-scan
// semantics; the identity half extends that port because EVO announces
// showroom variants as per-car mechanical presets inside the same ks_
// folder (PORTPLAN 62). File watching lives in Services/CarLogWatcher.
using System.Text.RegularExpressions;

namespace AceDrive.Core;

/// <summary>Maps ks_ car ids (from ACE logs or telemetry) to a spoken brand.</summary>
public static class CarBrand
{
    /// <summary>ks prefix (underscores) -> spoken brand (the wake word half).</summary>
    public static readonly IReadOnlyDictionary<string, string> Brands =
        new Dictionary<string, string>
        {
            ["abarth"] = "abarth",
            ["alfa_romeo"] = "alfa romeo",
            ["alpine"] = "alpine",
            ["audi"] = "audi",
            ["bmw"] = "bmw",
            ["caterham"] = "caterham",
            ["chevrolet"] = "chevrolet",
            ["dallara"] = "dallara",
            ["datsun"] = "datsun",
            ["ferrari"] = "ferrari",
            ["ford"] = "ford",
            ["honda"] = "honda",
            ["hyundai"] = "hyundai",
            ["ktm"] = "ktm",
            ["lamborghini"] = "lamborghini",
            ["lancia"] = "lancia",
            ["lotus"] = "lotus",
            ["maserati"] = "maserati",
            ["mazda"] = "mazda",
            ["mercedes"] = "mercedes",
            ["mini"] = "mini",
            ["peugeot"] = "peugeot",
            ["porsche"] = "porsche",
            ["renault"] = "renault",
            ["toyota"] = "toyota",
            ["volkswagen"] = "volkswagen",
        };

    private static readonly Regex KsId = new("ks_[a-z0-9_]+", RegexOptions.Compiled);
    private static readonly Regex CarLine = new(
        "(Creating car:|Loaded car|Game Started!|onSetPlayerCurrentCarCommand)",
        RegexOptions.Compiled);

    // Mechanical preset path segment on "Set new car" lines: the showroom
    // variant inside one car folder. Preset file names share the id alphabet.
    private static readonly Regex MechPresetPath = new(
        @"presets\\([a-z0-9_]+)\.mechanicalcarpreset", RegexOptions.Compiled);

    // Longest prefix first so ks_alfa_romeo_* never mis-matches a shorter key.
    private static readonly string[] Prefixes =
        [.. Brands.Keys.OrderBy(k => k, StringComparer.Ordinal).OrderByDescending(k => k.Length)];

    /// <summary>ks_mazda_mx5_na -> 'mazda'; null for unknown ids.</summary>
    public static string? BrandOf(string ksId)
    {
        string rest = ksId.StartsWith("ks_", StringComparison.Ordinal) ? ksId[3..] : ksId;
        foreach (string prefix in Prefixes)
        {
            if (rest == prefix || rest.StartsWith(prefix + "_", StringComparison.Ordinal))
            {
                return Brands[prefix];
            }
        }

        return null;
    }

    /// <summary>
    /// Brand for one ACE log line: the spoken brand when the line announces a
    /// known car ("Creating car:", "Game Started!", ...), null otherwise.
    /// </summary>
    public static string? BrandOfLogLine(string line)
    {
        if (!CarLine.IsMatch(line))
        {
            return null;
        }

        Match m = KsId.Match(line);
        return m.Success ? BrandOf(m.Value) : null;
    }

    /// <summary>
    /// ks id for one ACE log line: the id on a car-announcing line
    /// ("Creating car:", "Game Started!", ...), null otherwise. The brand
    /// twin (<see cref="BrandOfLogLine"/>) owns the spoken mapping; the
    /// identity twin (<see cref="IdentityOfLogLine"/>) owns capacity lookups.
    /// </summary>
    public static string? KsIdOfLogLine(string line)
    {
        if (!CarLine.IsMatch(line))
        {
            return null;
        }

        Match m = KsId.Match(line);
        return m.Success ? m.Value : null;
    }

    /// <summary>
    /// Full identity for one ACE log line: ks id plus the mechanical preset
    /// (showroom variant) when the line carries the "content\cars\&lt;id&gt;
    /// \presets\..." spawn path. Preset is null on the shorter announcing
    /// lines ("Creating car: ks_x"), which only name the folder.
    /// </summary>
    public static CarIdentity? IdentityOfLogLine(string line)
    {
        if (!CarLine.IsMatch(line))
        {
            return null;
        }

        Match m = KsId.Match(line);
        if (!m.Success)
        {
            return null;
        }

        Match p = MechPresetPath.Match(line);
        return new CarIdentity(m.Value, p.Success ? p.Groups[1].Value : null);
    }

    /// <summary>
    /// Identity of the current car scanning bottom-up: the newest announcing
    /// line wins, but a preset-less "Loaded car" echo inherits the preset
    /// from the spawn line above it when both name the same car (EVO emits
    /// "Set new car … presets\p.mechanicalcarpreset" then the echo — last
    /// line alone would lose the variant).
    /// </summary>
    public static CarIdentity? FindCurrentIdentity(IEnumerable<string> lines)
    {
        CarIdentity? current = null;
        foreach (string line in lines.Reverse())
        {
            if (IdentityOfLogLine(line) is not { } seen)
            {
                continue;
            }

            if (current is null)
            {
                current = seen;
                if (seen.MechPreset is not null)
                {
                    break;
                }
            }
            else if (seen.KsId == current.KsId && seen.MechPreset is not null)
            {
                current = current with { MechPreset = seen.MechPreset };
                break;
            }
        }

        return current;
    }
}
