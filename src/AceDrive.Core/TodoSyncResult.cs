// What one ToDo sync did (PORTPLAN item 46): whether anything moved, the
// subjects that completed for the first time (the care-package grant rides
// them), and the ledger version so a cached page knows to repaint.
namespace AceDrive.Core;

/// <summary>The result of <see cref="TodoLedger.Sync"/>.</summary>
public sealed record TodoSyncResult(
    bool Changed, IReadOnlyList<string> Completed, int Version);
