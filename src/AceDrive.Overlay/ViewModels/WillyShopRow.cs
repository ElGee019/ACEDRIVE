using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One coffee shop on the quest page (ToWhereIsWilly.md §3.4): the preformatted
/// name over its address, plus the door the row pins. The view stays dumb.
/// </summary>
public sealed record WillyShopRow(PointOfInterest Poi, string Name, string Address);
