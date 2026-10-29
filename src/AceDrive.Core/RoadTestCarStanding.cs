// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN.md item 49 and LATESTDRIVEBS2 RoadTestScreen's overall
// standings (the record book LoadGlobalBestPerCar builds: one row per car).
namespace AceDrive.Core;

/// <summary>
/// One row of the all-cars record book: a car, that car's best run (seconds or
/// metres, lower is better) and its place on the board. IsTestedCar marks the
/// car the page's test was driven in, which the page highlights.
/// </summary>
public readonly record struct RoadTestCarStanding(
    int Position,
    string CarName,
    double Value,
    bool IsTestedCar);
