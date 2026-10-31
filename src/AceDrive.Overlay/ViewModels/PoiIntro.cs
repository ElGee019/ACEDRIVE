namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One POI family's explanation page copy (TOZONES §4.5): the plural family
/// title the page names and the body it spells out. Copy comes from resx
/// through <see cref="PoiIntroCopy"/>; the page only paints it.
/// </summary>
public sealed record PoiIntro(string Title, string Body);
