// Car-id -> spoken wake brand, pure functions only (no file I/O).
// Spec source: companion carwatch.py, ported 1:1 including the log-scan
// semantics. File watching lives in Services/CarLogWatcher.
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

    /// <summary>Most recent ks id on a car line, scanning bottom-up.</summary>
    public static string? FindCurrentKs(IEnumerable<string> lines)
    {
        foreach (string line in lines.Reverse())
        {
            if (!CarLine.IsMatch(line))
            {
                continue;
            }

            Match m = KsId.Match(line);
            if (m.Success)
            {
                return m.Value;
            }
        }

        return null;
    }
}
