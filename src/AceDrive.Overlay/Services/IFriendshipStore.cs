// Familiarity storage (PORTPLAN item 45, Unity POIData.friendshipLevel):
// one level per resident, keyed case-insensitively on the base customer
// name (first + surname, no companion suffix). Small on purpose so fakes
// substitute cleanly in tests; the JSON file owns persistence.
namespace AceDrive.Overlay.Services;

public interface IFriendshipStore
{
    /// <summary>Level for a resident, 0 when never driven (first ride makes 1).</summary>
    int GetLevel(string ownerName);

    /// <summary>
    /// Applies one completed ride (+1) and returns the outcome for the
    /// receipt. Never throws (the file sink swallows IO).
    /// </summary>
    FriendshipOutcome RegisterRide(string ownerName);

    /// <summary>
    /// Applies a familiarity penalty (−1 per failed pickup). The dirty-car
    /// taxi refusal (item 33) is the first caller; the map's wrong-address
    /// cancellation joins it with the driving slice.
    /// </summary>
    void AddFamiliarity(string ownerName, int delta);
}
