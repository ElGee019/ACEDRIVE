// Driving-slice balance (PORTPLAN item 11): the door radius, the fare meter's
// speed clamp, the HUD distance step and a transient notice duration. Shipped
// values are the test-shell fallback; Data/taxi_rules.json is the source of
// truth, loaded by JsonTaxiRulesStore at startup.
namespace AceDrive.Core;

/// <summary>
/// Job-guidance rules the poll, the HUD and the meter share. All distances in
/// metres; times in seconds.
/// </summary>
public sealed record TaxiRules(
    double ArrivalRadiusMeters,
    double MaxMeterSpeedKmh,
    double DisplayDistanceStepMeters,
    double NoticeSeconds)
{
    /// <summary>Fallback used when the data file is missing or invalid.</summary>
    public static readonly TaxiRules Default = new(
        ArrivalRadiusMeters: 25.0,
        MaxMeterSpeedKmh: 400.0,
        DisplayDistanceStepMeters: 10.0,
        NoticeSeconds: 3.0);
}
