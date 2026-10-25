// ToDo subject ids (PORTPLAN item 46, port of Unity Todo/TodoSubject). The
// order IS the Unity enum order: the queue seeds and the popup order read it,
// so a new subject is appended beside its Unity sibling, never sorted by id.
// Two Unity subjects are absent by decision: ClubReputation belongs to the
// Mille Miglia (item 49, not ported) and VisitFuelStation to the declined fuel
// simulation (item 37). OpenDeliveryCareer is AceDrive's own addition: Max's
// grocery tip's gated follow-up (IUnlockedTodo), seated beside it.
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
    public const string VisitBank = "VisitBank";

    /// <summary>Every subject, in Unity's enum order.</summary>
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
