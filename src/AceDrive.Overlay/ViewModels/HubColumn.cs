namespace AceDrive.Overlay.ViewModels;

/// <summary>A career list: category title (itself a pick button) over its
/// hub levels. A column with no career shows the COMING SOON title and no
/// cells (Unity FillColumn). CanPick keeps the title button enabled only
/// while the list holds something that can actually be picked, mirroring
/// the per-cell interactable rule. Index drives the stacked-list spacing
/// (one blank row above the first list, two above the second); IsSelected
/// marks the list currently holding the pick, so moving between careers
/// visibly leaves the old one.</summary>
public sealed record HubColumn(
    int Index, string TitleLine, IReadOnlyList<HubCell> Cells, bool CanPick, bool IsSelected);
