namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// What one <see cref="WorldTicks.Tick"/> moved: the town's cells (the only
/// thing that makes the cached delivery board stale) and whether the appetite's
/// one-shot pancake event fired this tick. Constructor-style record struct so
/// the 10 Hz poll allocates nothing; the coordinator owns what each fact means
/// (a board repaint, the companion's nudge), never this leaf.
/// </summary>
public readonly record struct WorldTickReport(bool CityMoved, bool PancakeThreshold);
