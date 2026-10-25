using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The ToDo list as the session sees it (PORTPLAN item 46, port of Unity's
/// TodoManager MonoBehaviour): it assembles the career snapshot the
/// requirements read, activates a subject once its door exists, syncs the
/// ledger on the cold edges (boot, settle, shop entry, pin, purchase) and
/// banks its own file.
///
/// It is a collaborator, not a screen: the jobs page reads the lines and the
/// DELIVER CARE PACKAGES command it exposes, and nothing here knows about
/// pages. The care-package grant (PORTPLAN 21) will ride the completions this
/// sync returns.
/// </summary>
public sealed class TodoBoard : ITodoList
{
    private readonly TodoLedger _ledger;
    private readonly ITodoProgressStore _store;
    private readonly PoiDirectory _directory;
    private readonly IOnboardingStore _onboarding;
    private readonly DriverWallet _wallet;
    private readonly JobLedger _jobs;
    private readonly OperatingZone _zone;
    private readonly Garage _garage;
    private readonly DestinationPins _pins;
    private readonly VisitLedger _visits;
    private readonly DeliveryCareer _career;
    private readonly ILog _log;

    // The snapshot the lines were built from. The page repaints only when the
    // ledger version moves (i.e. after a sync), so caching it here keeps the
    // 10 Hz refresh off the onboarding store and the garage.
    private TodoFacts _facts;

    public TodoBoard(
        TodoTable table, ITodoProgressStore store, PoiDirectory directory,
        IOnboardingStore onboarding, DriverWallet wallet, JobLedger jobs,
        OperatingZone zone, Garage garage, DestinationPins pins, VisitLedger visits,
        DeliveryCareer career, ILog log)
    {
        _store = store;
        _directory = directory;
        _onboarding = onboarding;
        _wallet = wallet;
        _jobs = jobs;
        _zone = zone;
        _garage = garage;
        _pins = pins;
        _visits = visits;
        _career = career;
        _log = log;
        _ledger = TodoLedger.Resume(table, store.Load());
        _facts = Facts();
        AcknowledgeCommand = new RelayCommand(Acknowledge);
        // The boot edge: a returning career catches up on everything its save
        // already qualifies for, and a newly placed door joins rotation.
        _ = Sync();
    }

    /// <summary>DELIVER CARE PACKAGES: acknowledge the completed tips and refill the slots.</summary>
    public RelayCommand AcknowledgeCommand { get; }

    /// <summary>Changes whenever a sync, an acknowledgment or a rotation moved the list.</summary>
    public int Version => _ledger.Version;

    /// <summary>True while completed tips wait to be delivered.</summary>
    public bool HasCompleted => _ledger.HasCompleted;

    /// <summary>How many completed kits the page lists right now.</summary>
    public int CompletedCount => _ledger.CompletedCount;

    /// <summary>The four visible tips, ready for the page to format.</summary>
    public IReadOnlyList<TodoLine> Slots => Lines(_ledger.Slots(_facts));

    /// <summary>The completed tips the popup block lists, in subject order.</summary>
    public IReadOnlyList<TodoLine> CompletedLines => Lines(_ledger.CompletedTips(_facts));

    /// <summary>
    /// The cold-edge sync: activate the doors that now exist, flag every newly
    /// met subject and rotate a met tip out. Returns what completed so the
    /// caller (and, later, the care-package grant) can react.
    /// </summary>
    public TodoSyncResult Sync()
    {
        ActivatePlacedSubjects();
        _facts = Facts();
        TodoSyncResult result = _ledger.Sync(_facts);
        if (result.Changed)
        {
            _store.Save(_ledger.Snapshot());
        }

        if (result.Completed.Count > 0)
        {
            _log.LogInfo($"todo completed: {string.Join(", ", result.Completed)}");
        }

        return result;
    }

    /// <summary>
    /// One settled taxi or delivery job: every unfulfilled tip counts a turn
    /// and rotates out after six (Unity IncrementTurnCounter).
    /// </summary>
    public void RotateOnJob()
    {
        if (_ledger.RotateOnJob(_facts))
        {
            _store.Save(_ledger.Snapshot());
        }
    }

    private void Acknowledge()
    {
        if (_ledger.Acknowledge(_facts))
        {
            _store.Save(_ledger.Snapshot());
        }
    }

    private TodoFacts Facts()
    {
        OnboardingState progress = _onboarding.Load();
        return new TodoFacts(
            _wallet.CreditEarned,
            _wallet.Xp,
            _wallet.BankBalance,
            _jobs.TaxiCompleted,
            _jobs.DeliveryCompleted,
            _pins.Count,
            _zone.Km,
            _garage.Cars.Count,
            progress.MaxDeliveryIntroSeen,
            _visits.CarWash,
            _visits.CarDealer,
            _visits.InsuranceBroker,
            _wallet.HasEnteredBank,
            _career.Owned.Count > 0);
    }

    // Unity ActivateSubject: a dynamic POI visit joins rotation the first time
    // a door of its type is placed. The directory only grows, so this is a
    // one-way latch that the ledger records in its own save.
    private void ActivatePlacedSubjects()
    {
        foreach (string subject in TodoSubject.All)
        {
            ITodoRequirement requirement = TodoRequirements.Get(subject);
            if (requirement.PoiType is null || _ledger.IsActive(subject))
            {
                continue;
            }

            if (FindDoor(requirement.PoiType) is null)
            {
                continue;
            }

            if (_ledger.Activate(subject))
            {
                _log.LogInfo($"todo activated: {subject} ({requirement.PoiType} placed)");
            }
        }
    }

    private PointOfInterest? FindDoor(string type)
    {
        foreach (PointOfInterest poi in _directory.All)
        {
            if (string.Equals(poi.Type, type, StringComparison.OrdinalIgnoreCase))
            {
                return poi;
            }
        }

        return null;
    }

    private List<TodoLine> Lines(IReadOnlyList<TodoSlotView> views)
    {
        var lines = new List<TodoLine>(views.Count);
        foreach (TodoSlotView view in views)
        {
            lines.Add(LineOf(view));
        }

        return lines;
    }

    // A visit tip names the door; when no door of that type is known yet the
    // authored sentence stands in (Unity GetTipText's fallback).
    private TodoLine LineOf(TodoSlotView view)
    {
        if (view.PoiType is not { } type)
        {
            return new TodoLine(view.CopyKey, view.Target, view.Current, null);
        }

        if (FindDoor(type) is { } door)
        {
            return new TodoLine(view.CopyKey, view.Target, view.Current, door.Name);
        }

        string fallback = TodoRequirements.Get(view.SubjectId).FallbackKey ?? view.CopyKey;
        return new TodoLine(fallback, view.Target, view.Current, null);
    }
}
