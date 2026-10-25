// Intro page progress (PORTPLAN Career Start items 2-5): the walk banks the
// page it is on so closing the overlay resumes there instead of replaying.
namespace AceDrive.Core;

/// <summary>
/// The introduction page the save was last on, in the order the walk visits
/// them. The completed walk is not a page: <see cref="OnboardingState.IsComplete"/>
/// ends the introduction, and the next launch opens on the menu instead of a
/// walk page.
/// </summary>
public enum OnboardingStage
{
    /// <summary>Unit/language page: the first page of a fresh install.</summary>
    Preferences = 0,

    /// <summary>Name + home address form.</summary>
    Opening = 1,

    /// <summary>Favorite brand pick.</summary>
    Brand = 2,

    /// <summary>Voice-info page that hands over to the birthday dialogue.</summary>
    VoiceInfo = 3,

    /// <summary>Birthday dialogue: grandma gives the car and the errand away.</summary>
    Dialogue = 4,

    /// <summary>
    /// Grandma's car page, the walk's closing page: the player changes into the
    /// car she just handed over before the panel drops onto the mini window.
    /// </summary>
    GrandmaCar = 5,
}
