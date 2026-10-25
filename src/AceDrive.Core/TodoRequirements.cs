// The ToDo requirement table (PORTPLAN item 46, port of Unity
// Todo/TodoSubjectData): one rule per subject, looked up by id. A new subject
// is registered here and nowhere else — the ledger, the page and the queue
// never branch on a subject id.
namespace AceDrive.Core;

/// <summary>Subject id → its requirement, in Unity's enum order.</summary>
public static class TodoRequirements
{
    // WHY one visit rule per door instead of one generic flag: the tip names
    // the door's own POI type, so each entry carries its own flag reader.
    private static readonly Dictionary<string, ITodoRequirement> BySubject =
        new(StringComparer.Ordinal)
        {
            [TodoSubject.CrEarnings] = new CrEarningsRequirement(),
            [TodoSubject.XpEarnings] = new XpEarningsRequirement(),
            [TodoSubject.BankStandings] = new BankStandingsRequirement(),
            [TodoSubject.TaxiJobs] = new TaxiJobsRequirement(),
            [TodoSubject.SetDestinationsManually] = new SetDestinationsRequirement(),
            [TodoSubject.OperatingZone] = new OperatingZoneRequirement(),
            [TodoSubject.OwnCars] = new OwnCarsRequirement(),
            [TodoSubject.GoToMaxGroceryStore] = new VisitTodoRequirement(
                TodoSubject.GoToMaxGroceryStore, "TodoMaxGrocery", "TodoMaxGrocery",
                facts => facts.MaxGroceryIntroSeen),
            [TodoSubject.OpenDeliveryCareer] = new FollowUpTodoRequirement(
                TodoSubject.OpenDeliveryCareer, TodoSubject.GoToMaxGroceryStore,
                "TodoOpenDeliveryCareer", facts => facts.DeliveryCareerOpened),
            [TodoSubject.VisitCarWash] = new VisitTodoRequirement(
                TodoSubject.VisitCarWash, "TodoEnterPoi", "TodoVisitCarWash",
                facts => facts.CarWashEntered, PoiTypes.CarWash),
            [TodoSubject.VisitCarDealer] = new VisitTodoRequirement(
                TodoSubject.VisitCarDealer, "TodoEnterPoi", "TodoVisitCarDealer",
                facts => facts.CarDealerEntered, PoiTypes.Dealer),
            [TodoSubject.VisitInsuranceBroker] = new VisitTodoRequirement(
                TodoSubject.VisitInsuranceBroker, "TodoEnterPoi", "TodoVisitInsurance",
                facts => facts.InsuranceBrokerEntered, PoiTypes.Insurance),
            [TodoSubject.DeliveryJobsCompleted] = new DeliveryJobsRequirement(),
            [TodoSubject.VisitBank] = new VisitTodoRequirement(
                TodoSubject.VisitBank, "TodoVisitBank", "TodoVisitBank",
                facts => facts.BankEntered),
        };

    /// <summary>The rule for a subject; unknown ids are a programming error.</summary>
    public static ITodoRequirement Get(string subject) =>
        BySubject.TryGetValue(subject, out ITodoRequirement? requirement)
            ? requirement
            : throw new ArgumentOutOfRangeException(
                nameof(subject), subject, "no ToDo requirement registered");

    public static bool IsRegistered(string? subject) =>
        !string.IsNullOrEmpty(subject) && BySubject.ContainsKey(subject);

    /// <summary>
    /// Unity TodoSubjectData.IsInitiallyActive: the four POI-visit subjects
    /// wait for their door to be placed, and a follow-up subject waits for its
    /// gate to complete; every other subject is live from the first career day.
    /// </summary>
    public static bool IsInitiallyActive(string subject)
    {
        if (subject is TodoSubject.VisitCarWash
            or TodoSubject.VisitCarDealer
            or TodoSubject.VisitInsuranceBroker
            or TodoSubject.VisitBank)
        {
            return false;
        }

        return Get(subject) is not IUnlockedTodo;
    }
}
