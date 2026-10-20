namespace AceDrive.Core;

/// <summary>Outcome of one advance on the hunger meter (Unity
/// GameClockManager.UpdateHunger's per-hour effects on one line): the
/// current reading, whether anything moved that way (the transition worth
/// logging), and whether the pancake-threshold event fired on the last
/// crossing.</summary>
/// <param name="Hunger">The meter's reading after the advance.</param>
/// <param name="ThresholdFired">True when this crossing landed on the
/// pancake threshold with pancakes available and the one-shot event had not
/// fired before.</param>
/// <param name="Changed">True when the reading moved at all — Unity clamps
/// at the cap before logging, so an hour passing at full hunger reports
/// nothing.</param>
public sealed record HungerReport(int Hunger, bool ThresholdFired, bool Changed);
