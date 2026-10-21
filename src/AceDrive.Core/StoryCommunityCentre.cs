// Fixed story community centre (PORTPLAN item 54): identity only, because the
// house is drawn on a random street and banked with the onboarding progress.
namespace AceDrive.Core;

/// <summary>
/// The community centre where the tour ends and the taxi license is signed.
/// </summary>
public static class StoryCommunityCentre
{
    public static PoiDefinition Definition { get; } = new(
        "community_centre", "Community Centre", PoiTypes.CommunityCentre);
}
