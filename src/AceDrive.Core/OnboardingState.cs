// Onboarding data (PORTPLAN Career Start items 2-5): favorite brand ->
// voice info -> birthday dialogue -> grandma's car -> errand. Progress
// persists via IOnboardingStore.
namespace AceDrive.Core;

/// <summary>
/// Persisted onboarding progress. The walk banks the page it is on
/// (<see cref="Stage"/>) so a relaunch resumes there; the story POIs, both
/// pickups and the pancake/community-centre visits are banked too, so a
/// returning player drives back to the same doors and resumes on the right
/// leg of the tour. The random story addresses are banked once and reused
/// later (Unity's maxsStoreAddress / communityCentreAddress), so a relaunch
/// never moves a door the player has already visited.
/// </summary>
public sealed record OnboardingState(
    string? FavoriteBrand,
    string? GrandmaCarId,
    bool BirthdayDone,
    AddressLocation? JustinAddress = null,
    AddressLocation? AnnabellaAddress = null,
    bool JustinPickedUp = false,
    bool AnnabellaPickedUp = false,
    bool PancakesDone = false,
    string? MaxStoreId = null,
    AddressLocation? MaxStoreAddress = null,
    AddressLocation? CommunityCentreAddress = null,
    // The door-only La Strada office: placed on a street connecting the tour's
    // neighbourhood so the addresses page can list it before its interior is
    // ported. Null on saves written before the door existed.
    AddressLocation? LaStradaAddress = null,
    bool CommunityCentreDone = false,
    bool MaxErrandDone = false,
    // Max's first store visit after the introduction shows the delivery offer
    // and pays the intro CredP once (Unity hasSeenMaxJobIntro). Kept apart
    // from MaxErrandDone: the birthday errand visit happens before the tour
    // ends and must not pay the gift. A new field, not the old
    // MaxCredPGranted: saves stamped by the errand-time grant stay unaccepted,
    // so those players still see the offer on their next Max visit.
    bool MaxDeliveryIntroSeen = false,
    // Last argument so saves written before the stage existed deserialize
    // with the fresh default (Preferences) instead of failing.
    OnboardingStage Stage = OnboardingStage.Preferences,
    // The mic permission the voice page asks for. Null means the question was
    // never answered, which is the state of a fresh install and of every save
    // written before the page existed; VoiceAllowed below tells the two apart.
    bool? VoiceConsent = null)
{
    public static readonly OnboardingState Fresh = new(null, null, false);
    public bool IsComplete => BirthdayDone;

    /// <summary>
    /// True when the microphone may open. An explicit CONFIRM allows it and an
    /// explicit NO THANK YOU keeps it off; an unanswered save is grandfathered
    /// only once it already walked past the voice page (or finished the intro),
    /// so an update never silently mutes an existing profile. A fresh install
    /// stays off until the page is answered, because the mic must never open
    /// before the player consented.
    /// </summary>
    public bool VoiceAllowed =>
        VoiceConsent ?? (IsComplete || Stage is OnboardingStage.Dialogue or OnboardingStage.GrandmaCar);
}
