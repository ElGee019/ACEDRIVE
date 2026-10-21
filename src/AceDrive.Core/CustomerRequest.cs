// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Spec source: PORTPLAN item 11 (Unity TaxiJobGenerator special requests).
namespace AceDrive.Core;

/// <summary>
/// One special request the passenger makes at the pickup. Text is already
/// composed for the customer identity; a quiz carries its three options and
/// the correct one so the page never re-derives gameplay data.
/// </summary>
public sealed record CustomerRequest(
    CustomerRequestKind Kind,
    string Text,
    CarStatQuestion? Quiz = null)
{
    public bool IsSpeedChallenge => Kind == CustomerRequestKind.SpeedChallenge;
}
