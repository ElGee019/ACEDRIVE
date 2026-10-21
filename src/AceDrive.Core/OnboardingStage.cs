// Intro page progress (PORTPLAN Career Start items 2-5): the walk banks the
// page it is on so closing the overlay resumes there instead of replaying.
namespace AceDrive.Core;

/// <summary>
/// The introduction page the save was last on. The completed walk is not a
/// page: <see cref="OnboardingState.IsComplete"/> ends the introduction, and
/// the next launch opens on the menu instead of a walk page.
/// </summary>
public enum OnboardingStage
{
    /// <summary>Unit/language page: the first page of a fresh install.</summary>
    Preferences = 0,

    /// <summary>Name + home address form.</summary>
    Opening = 1,

    /// <summary>Favorite brand pick.</summary>
    Brand = 2,

    /// <summary>Grandma's car pick.</summary>
    GrandmaCar = 3,

    /// <summary>Voice-info page that hands over to the birthday dialogue.</summary>
    VoiceInfo = 4,

    /// <summary>Birthday dialogue (last page of the walk).</summary>
    Dialogue = 5,
}
