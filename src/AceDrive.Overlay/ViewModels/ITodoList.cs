// The jobs page's read-only slice of the ToDo list (PORTPLAN item 46): the
// four tips, the completed kits and a version to guard the repaint. The page
// never sees the ledger or the sync — the board owns both.
namespace AceDrive.Overlay.ViewModels;

/// <summary>What the jobs page reads off <see cref="TodoBoard"/>.</summary>
public interface ITodoList
{
    /// <summary>Moves whenever the list changed, so the page knows to repaint.</summary>
    int Version { get; }

    /// <summary>True while completed tips wait to be delivered.</summary>
    bool HasCompleted { get; }

    /// <summary>How many completed kits are waiting.</summary>
    int CompletedCount { get; }

    /// <summary>The four visible tips.</summary>
    IReadOnlyList<TodoLine> Slots { get; }

    /// <summary>The completed tips the popup block lists.</summary>
    IReadOnlyList<TodoLine> CompletedLines { get; }
}
