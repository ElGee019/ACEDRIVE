// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity CustomerRequestScreen quiz options).
namespace AceDrive.Core;

/// <summary>
/// One ready-to-show quiz: the question text, the three option values and
/// which one is correct. The page formats the values with the category's
/// unit key, so Core never builds display sentences.
/// </summary>
public sealed record CarStatQuestion(
    CarStatCategory Category,
    string Text,
    IReadOnlyList<double> Options,
    int CorrectIndex);
