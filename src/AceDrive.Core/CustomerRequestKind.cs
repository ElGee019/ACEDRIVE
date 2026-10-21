// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity TaxiJobGenerator special requests).
namespace AceDrive.Core;

/// <summary>
/// The kind of special request a drawn customer may make. WhatCar is
/// deliberately absent until a passing-car image exists for the whole roster;
/// the tier-3 slot is quiz-only until then.
/// </summary>
public enum CustomerRequestKind
{
    Passive,
    SpeedChallenge,
    CarQuiz,
}
