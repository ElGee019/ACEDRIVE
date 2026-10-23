using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// The banked POI directory behind the addresses page. A new discovered door
/// is written once, so the list survives a restart without re-drawing an
/// address the player already knows.
/// </summary>
public interface IPoiDirectoryStore
{
    /// <summary>Every banked door; an empty list when the file is absent or unreadable.</summary>
    IReadOnlyList<PointOfInterest> Load();

    /// <summary>Banks the current list. Never throws.</summary>
    void Save(IReadOnlyList<PointOfInterest> pois);
}
