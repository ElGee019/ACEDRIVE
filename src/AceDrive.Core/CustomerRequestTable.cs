// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity TaxiJobGenerator tiers).
using System.Text.Json.Serialization;

namespace AceDrive.Core;

/// <summary>
/// The loaded request content: suffix-triggered passive lines with their tip
/// conditions, the quiz categories, the speed-challenge line and the tier
/// chances/timer, plus the thresholds the passive asks' live limits use.
/// Shipped values live in Data/customer_requests.json; this default is the
/// test-shell fallback and deliberately asks nothing, so a missing file never
/// invents a request. WHY 19 s instead of Unity's 14: the port added five
/// seconds of reading time to the answer window, and every request kind
/// (passive, speed challenge, quiz) runs on it.
/// </summary>
public sealed record CustomerRequestTable(
    IReadOnlyDictionary<string, SuffixRequest> SuffixRequests,
    IReadOnlyList<CarStatCategory> QuestionCategories,
    string SpeedChallengeText,
    double Tier2Chance,
    double Tier3Chance,
    int TimerSeconds,
    [property: JsonPropertyName("monitor")] RideMonitorRules MonitorRules)
{
    public static readonly CustomerRequestTable Empty = new(
        new Dictionary<string, SuffixRequest>(StringComparer.Ordinal),
        [],
        SpeedChallengeText: string.Empty,
        Tier2Chance: 0.0,
        Tier3Chance: 0.0,
        TimerSeconds: 19,
        MonitorRules: RideMonitorRules.Default);
}
