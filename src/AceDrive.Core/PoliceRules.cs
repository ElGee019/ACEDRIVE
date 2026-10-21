// Speed-challenge police balance (PORTPLAN item 11, Phase C). Shipped values
// are the test-shell fallback; Data/police.json is the source of truth, loaded
// by JsonPoliceRulesStore at startup.
namespace AceDrive.Core;

/// <summary>
/// The patrol plan's shape: how many patrols a job gets, how close a car has
/// to pass to be caught, the posted limit, the keep-out around both stops and
/// the distance at which the HUD warns. All distances in metres, speeds in
/// km/h. Unity checks the whole street; this loop uses patrol points because
/// two streets would otherwise fine every accepted challenge.
/// </summary>
public sealed record PoliceRules(
    int PatrolsPerJob,
    double PatrolRadiusMeters,
    int SpeedLimitKmh,
    double ExcludedStopRadiusMeters,
    double WarningRadiusMeters)
{
    /// <summary>Fallback used when the data file is missing or invalid.</summary>
    public static readonly PoliceRules Default = new(
        PatrolsPerJob: 1,
        PatrolRadiusMeters: 30.0,
        SpeedLimitKmh: 50,
        ExcludedStopRadiusMeters: 60.0,
        WarningRadiusMeters: 120.0);
}
