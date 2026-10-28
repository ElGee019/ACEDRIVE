// Pure gameplay logic. No WPF / Windows / telemetry / backend references.
// Kept dependency-free on purpose so a future Lua port can transliterate
// this file instead of the UI. Spec source: PORTPLAN.md item 18 (Unity hub
// purchase) + item 51 (the save that had not landed yet).
namespace AceDrive.Core;

/// <summary>
/// One-time recovery for a career banked by a build that kept it only in
/// memory: the hub pickup door was already written to the address directory at
/// purchase, so a save with no career block can rebuild the owned hubs from
/// those doors. The CredP balance and the per-hub delivery counts were never
/// written anywhere and cannot be recovered: the rebuilt career starts on 0
/// CredP and the ramp's first step. Null when no hub door survives.
/// </summary>
public static class DeliveryCareerRecovery
{
    public static DeliveryCareerState? Recover(
        HubCatalog hubs, IEnumerable<PointOfInterest> doors)
    {
        ArgumentNullException.ThrowIfNull(hubs);
        ArgumentNullException.ThrowIfNull(doors);
        var owned = new List<OwnedHub>();
        foreach (PointOfInterest door in doors)
        {
            if (door.Type != PoiTypes.Hub || door.Id.Length == 0) continue;
            if (Contains(owned, door.Id)) continue;
            owned.Add(Rebuild(hubs, door));
        }

        return owned.Count == 0 ? null : new DeliveryCareerState(0, owned);
    }

    // The content usually still names the bought level, so the ramp resumes on
    // the right tier/level row. A content edit that renamed it keeps the door's
    // identity and the ramp's first row rather than forgetting the purchase.
    private static OwnedHub Rebuild(HubCatalog hubs, PointOfInterest door)
    {
        foreach (HubTier tier in hubs.Tiers)
        {
            foreach (HubCareer career in tier.Careers)
            {
                if (!string.Equals(career.Id, door.Category, StringComparison.Ordinal)) continue;
                for (int i = 0; i < career.Levels.Count; i++)
                {
                    HubLevel level = career.Levels[i];
                    if (string.Equals(level.Name, door.Name, StringComparison.Ordinal))
                    {
                        return new OwnedHub(
                            level.Name, career.Id, level.Cost, tier.Id, i + 1,
                            door.OwnerName, door.Id);
                    }
                }
            }
        }

        return new OwnedHub(
            door.Name, door.Category, 0, 1, 1, door.OwnerName, door.Id);
    }

    private static bool Contains(List<OwnedHub> owned, string key)
    {
        foreach (OwnedHub hub in owned)
        {
            if (string.Equals(hub.Key, key, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
