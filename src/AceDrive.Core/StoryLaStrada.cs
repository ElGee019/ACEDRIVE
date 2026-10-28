// Door-only La Strada (PORTPLAN item 37 declined its magazine page): identity
// only, because the house is drawn on a connecting street and banked with the
// onboarding progress. The address page lists it; no interior claims it yet.
namespace AceDrive.Core;

/// <summary>
/// La Strada, the car magazine whose story door closes the tour's street
/// neighbourhood. A real interior can land later without moving this door.
/// </summary>
public static class StoryLaStrada
{
    public static PoiDefinition Definition { get; } = new(
        "la_strada", "La Strada", PoiTypes.Magazine);
}
