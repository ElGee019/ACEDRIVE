// One tip line as the jobs page renders it: the copy key, its values and —
// for a visit tip — the door name that fills {0}. The shell resolves the name
// from the POI directory; the page only formats.
namespace AceDrive.Overlay.ViewModels;

/// <summary>A tip ready for <c>Strings.Format</c>.</summary>
public sealed record TodoLine(
    string CopyKey, double Target, double Current, string? Placeholder);
