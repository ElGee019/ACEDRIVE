// Fixed story home (PORTPLAN items 5/27): grandma's house where the tour
// ends. The address is the player's own profile, so only the identity lives
// here; the flow resolves the door against the profile and the address map.
namespace AceDrive.Core;

/// <summary>Grandma's house, the final stop of the introduction tour.</summary>
public static class StoryHome
{
    public static PoiDefinition Definition { get; } = new(
        "home", "Grandma's House", PoiTypes.Home);
}
