// The player snapshot a ToDo requirement reads (PORTPLAN item 46, port of
// Unity TodoEvaluators' PlayerData reads). Pure data: the collaborator that
// owns each number hands it over as a value, so no requirement ever reaches
// into a wallet, a garage or a store. The poll never builds one; the sync runs
// on cold edges only (settle, shop entry, pin, boot).
namespace AceDrive.Core;

/// <summary>Everything the ToDo requirements ask about the career.</summary>
public sealed record TodoFacts(
    double CreditEarned,
    double Xp,
    double Bank,
    int TaxiJobs,
    int DeliveryJobs,
    int ManuallySetDestinations,
    double ZoneKm,
    int OwnedCars,
    bool MaxGroceryIntroSeen,
    bool CarWashEntered,
    bool CarDealerEntered,
    bool InsuranceBrokerEntered,
    bool BankEntered,
    bool DeliveryCareerOpened);
