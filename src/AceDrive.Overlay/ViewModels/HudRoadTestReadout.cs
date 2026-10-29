// The companion's road-test block (PORTPLAN item 49): the task sentence a page
// hands over plus the live counter/timer reading, and — while the photo mission
// runs — the PHOTOSHOOT button that takes the shot. Kept as a small record
// beside the HUD interface it belongs to, like HudDestinationCopy.
namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// What the companion paints while a road-test leg runs. <paramref name="TaskKey"/>
/// is a resource key; when <paramref name="TargetKmh"/> is above zero the key
/// carries the target speed and its unit as placeholders, so a language or
/// miles/kilometres switch repaints the sentence instead of leaving
/// host-formatted copy behind — zero names a line with no placeholders (the
/// halt ask, or the photo mission's drive-anywhere brief).
/// <paramref name="ClockText"/> is the already painted reading — the countdown
/// digit ("5"), the running time ("0.000") or the brake distance ("12.3 m");
/// empty while the photo mission runs, because a shoot is not timed.
/// <paramref name="Photoshoot"/> offers the button that takes the shot wherever
/// the car is parked.
/// </summary>
public sealed record HudRoadTestReadout(
    string TaskKey, string ClockText, double TargetKmh, bool Photoshoot = false);
