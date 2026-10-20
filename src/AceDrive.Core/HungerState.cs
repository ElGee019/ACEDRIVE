namespace AceDrive.Core;

/// <summary>Saved hunger reading (Unity PlayerData.hunger +
/// hungerEventTriggered): the meter resumes across sessions instead of
/// re-seeding at 0.</summary>
/// <param name="Hunger">Reading to restore, clamped to the rules' cap.</param>
/// <param name="ThresholdTriggered">Whether the one-shot pancake event has
/// already fired (Unity never unsets hungerEventTriggered).</param>
public sealed record HungerState(int Hunger, bool ThresholdTriggered);
