// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity TaxiJobGenerator special requests).
namespace AceDrive.Core;

/// <summary>
/// The kind of special request a drawn customer may make. WhatCar is
/// dropped: the overlay has no passing-car image for the roster, so
/// the tier-3 slot is quiz-only.
/// </summary>
public enum CustomerRequestKind
{
    Passive,
    SpeedChallenge,
    CarQuiz,
}
