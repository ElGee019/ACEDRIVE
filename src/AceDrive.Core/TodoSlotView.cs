// One subject as the page renders it (PORTPLAN item 46): the copy key, the
// target, the career reading and — for a visit tip — the POI type whose name
// fills the placeholder. The shell resolves that name; Core never knows one.
namespace AceDrive.Core;

/// <summary>A tip ready to format: <c>{0}</c> is the target or the door name.</summary>
public sealed record TodoSlotView(
    string SubjectId, string CopyKey, double Target, double Current, string? PoiType);
