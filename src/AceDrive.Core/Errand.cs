namespace AceDrive.Core;

/// <summary>
/// The story errand's completion rule: an errand is done once every item its
/// POI guarantees is carried in the trunk. Pure data in, pure answer out, so
/// the host only asks the question and a future EVO Lua port shares the rule.
/// </summary>
public static class Errand
{
    /// <summary>
    /// True when the POI promises at least one item and all of them are
    /// carried. A POI without guaranteed items is never "complete": ordinary
    /// shops have no story phase to advance.
    /// </summary>
    public static bool IsComplete(PointOfInterest poi, Trunk trunk)
    {
        ArgumentNullException.ThrowIfNull(poi);
        ArgumentNullException.ThrowIfNull(trunk);
        if (poi.GuaranteedItems is not { Count: > 0 } required)
        {
            return false;
        }

        foreach (string item in required)
        {
            if (!trunk.Contains(item))
            {
                return false;
            }
        }

        return true;
    }
}
