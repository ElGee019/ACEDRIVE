using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The live delivery board: the catalogue a placed shop appends its row to at
/// runtime. A holder instead of a field the shell reassigns, so the board page,
/// the run planner and the settle read one instance and no collaborator is left
/// holding a snapshot that a newly opened door never reached.
///
/// The growth that fills it (the shop doors) moves into <c>PoiWorld</c> in S3;
/// until then the shell owns the only call site.
/// </summary>
public sealed class DeliveryBoard
{
    public DeliveryBoard(DeliveryCatalog catalog) => Catalog = catalog;

    /// <summary>How the board stands now; a newly connected shop replaces it.</summary>
    public DeliveryCatalog Catalog { get; private set; }

    /// <summary>Connects one placed shop. The board only ever grows.</summary>
    public void Add(DeliveryStore store) => Catalog = Catalog.WithStore(store);
}
