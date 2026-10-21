// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity ProceduralQuestionService).
namespace AceDrive.Core;

/// <summary>
/// The numeric car columns the quiz may ask about. The Unity roster also has
/// a designer column, but this portfolio's CarSpec does not, so that category
/// is absent (Unity skips an empty designer too).
/// </summary>
public enum CarStat
{
    MassKg,
    Horsepower,
    TopSpeedKmh,
    EngineCc,
    FuelPer100Km,
    Price,
}
