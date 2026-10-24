// Onboarding data (PORTPLAN Career Start items 2-5): favorite brand ->
// grandma's car -> birthday dialogue -> errand. Progress persists via
// IOnboardingStore.
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
    OnboardingStage Stage = OnboardingStage.Preferences)
{
    public static readonly OnboardingState Fresh = new(null, null, false);
    public bool IsComplete => BirthdayDone;
}
