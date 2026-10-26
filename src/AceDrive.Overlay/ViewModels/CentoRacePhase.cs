// Cento Miglia race phases (PORTPLAN item 49, port of Unity
// MilleMigliaPhase): the entry's own state between the event page and the
// receipt. Idle is expressed by the flow holding no race, so None is the only
// "not running" value.
namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Where one Cento Miglia entry is: driving to the start, typing the
/// registration at the start, or racing to the finish. Unity also models a
/// Proposal and a Completion phase; the port reaches the start from the event
/// page and hands the completion straight to the receipt, so neither needs a
/// state of its own.
/// </summary>
public enum CentoRacePhase
{
    None,
    ToStart,
    Verification,
    Racing,
}
