namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// What one completed mechanic action moved, handed to the host so it can bank
/// the spend and log it. The car models already carry the state change (the
/// parts were serviced, the engine repaired/replaced, the odometer reset) —
/// this is only the receipt of the action, so the page itself writes nothing.
/// </summary>
public sealed record MechanicRepair(
    string Kind,
    double Cost,
    int PartsReplaced,
    bool EngineReplaced);
