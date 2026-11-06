// ToDo subject ids (PORTPLAN item 46, port of Unity Todo/TodoSubject). The
// order IS the Unity enum order: the queue seeds and the popup order read it,
// so a new subject is placed beside its Unity sibling, never sorted by id.
// ClubReputation joins after the delivery tips rather than in Unity's exact
// (unrecoverable) slot: reputation only moves at a settled Cento Miglia, and a
// tip nobody can complete inside the first career week makes a poor early
// slot. One Unity subject is still absent: VisitFuelStation, because the port
// has no fuel-station door to enter — the fill receipt (item 37) is the fuel
// sim's money side, not a visit. OpenDeliveryCareer is AceDrive's own
// addition: Max's grocery tip's gated follow-up (IUnlockedTodo), beside it.
namespace AceDrive.Core;

/// <summary>Stable ids for every ToDo subject the career can complete.</summary>
public static class TodoSubject
{
    public const string CrEarnings = "CrEarnings";
    public const string XpEarnings = "XpEarnings";
    public const string BankStandings = "BankStandings";
    public const string TaxiJobs = "TaxiJobs";
    public const string SetDestinationsManually = "SetDestinationsManually";
    public const string OperatingZone = "OperatingZone";
    public const string OwnCars = "OwnCars";
    public const string GoToMaxGroceryStore = "GoToMaxGroceryStore";
    public const string OpenDeliveryCareer = "OpenDeliveryCareer";
    public const string VisitCarWash = "VisitCarWash";
    public const string VisitCarDealer = "VisitCarDealer";
    public const string VisitInsuranceBroker = "VisitInsuranceBroker";
    public const string DeliveryJobsCompleted = "DeliveryJobsCompleted";
    public const string ClubReputation = "ClubReputation";
    public const string VisitBank = "VisitBank";

    /// <summary>Every subject, in the order the ledger queues them.</summary>
    public static readonly IReadOnlyList<string> All =
    [
        CrEarnings,
        XpEarnings,
        BankStandings,
        TaxiJobs,
        SetDestinationsManually,
        OperatingZone,
        OwnCars,
        GoToMaxGroceryStore,
        OpenDeliveryCareer,
        VisitCarWash,
        VisitCarDealer,
        VisitInsuranceBroker,
        DeliveryJobsCompleted,
        ClubReputation,
        VisitBank,
    ];

    public static bool IsKnown(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return false;
        }

        foreach (string subject in All)
        {
            if (string.Equals(subject, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
