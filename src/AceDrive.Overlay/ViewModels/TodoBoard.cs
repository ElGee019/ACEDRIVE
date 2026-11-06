using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The ToDo list as the session sees it (PORTPLAN item 46, port of Unity's
/// TodoManager MonoBehaviour): it assembles the career snapshot the
/// requirements read, activates a subject once its door exists, syncs the
/// ledger on the cold edges (boot, settle, shop entry, purchase) and banks its
/// own file.
///
/// It is a collaborator, not a screen: the jobs page reads the lines and the
/// care-kit count, and nothing here knows about pages. The care-package grant
/// (PORTPLAN 21) rides the completions this sync returns — one kit per genuine
/// completion, dropped at a seeded store draw, banked with the driver save.
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
    private readonly VisitLedger _visits;
    private readonly DeliveryCareer _career;
    private readonly CarePackageLedger _carePackages;
    private readonly Random _careRng;
    private readonly Action _saveDriver;
    private readonly ILog _log;

    // The snapshot the lines were built from. The page repaints only when the
    // ledger version moves (i.e. after a sync), so caching it here keeps the
    // 10 Hz refresh off the onboarding store and the garage.
    private TodoFacts _facts;

    public TodoBoard(
        TodoTable table, ITodoProgressStore store, PoiDirectory directory,
        IOnboardingStore onboarding, DriverWallet wallet, JobLedger jobs,
        OperatingZone zone, Garage garage, VisitLedger visits,
        DeliveryCareer career, CarePackageLedger carePackages, Random careRng,
        Action saveDriver, ILog log)
    {
        _store = store;
        _directory = directory;
        _onboarding = onboarding;
        _wallet = wallet;
        _jobs = jobs;
        _zone = zone;
        _garage = garage;
        _visits = visits;
        _career = career;
        _carePackages = carePackages;
        _careRng = careRng;
        _saveDriver = saveDriver;
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

    /// <summary>Every care kit the career still owes (store shelves + car).</summary>
    public int CarePackageCount => _carePackages.Total;

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

        // WHY the grant banks before the ToDo file: the two states live in
        // different files, so a kill between the writes must never bank a tip
        // whose kit was not granted. A kill after the driver save re-flags the
        // tip on the next sync and grants a second kit — capped, and better
        // than losing the reward the tip just earned.
        if (result.Completed.Count > 0)
        {
            _log.LogInfo($"todo completed: {string.Join(", ", result.Completed)}");
        }

        OweCarePackages(result.Completed.Count);
        PlaceOwedKits();

        if (result.Changed)
        {
            _store.Save(_ledger.Snapshot());
        }

        return result;
    }

    // One kit per genuine completion (Unity TodoManager.SyncWithPlayer): the
    // grant lives here because most completions happen while the map page is
    // hidden, and every cold-edge sync funnels through this one method. The
    // store draw is seeded so a test can reproduce it; the cap can drop a grant
    // without dropping the tip, exactly like Unity's MaxCarePackages.
    //
    // A completion never loses its kit: when no shop door can take it, the kit
    // is owed in the ledger and placed by the next sync that can. The completed
    // block is the only evidence an older save leaves for a kit that was
    // dropped — it is acknowledged when a run starts, so its rows are exactly
    // the completions this save has not delivered yet.
    private void OweCarePackages(int completed)
    {
        int banked = _carePackages.Defer(completed);

        // Then the backlog a save may carry: the completed block is the only
        // evidence an older build leaves for a kit it dropped. Deferring today's
        // completions first keeps them out of this difference, so a new
        // completion and an old debt are banked separately, never twice.
        int owed = _ledger.CompletedCount - _carePackages.Total - _carePackages.Unplaced;
        banked += _carePackages.Defer(owed);

        if (banked > 0)
        {
            _log.LogInfo(
                $"care package owed: {banked} kit(s) wait for a shop door " +
                $"({_carePackages.Unplaced} unplaced)");
        }
    }

    // Places every owed kit on a shop shelf. The store draw runs over the whole
    // address book, not the taxi zone: the player has already driven to these
    // doors, and a shop that joined the directory before the zone expanded still
    // has to be able to hold the kit its completion earned.
    private void PlaceOwedKits()
    {
        IReadOnlyList<PointOfInterest> stores =
            CarePackageStores.Eligible(_directory.All, _zone.Map);

        bool granted = false;
        while (_carePackages.Unplaced > 0 && !_carePackages.IsFull)
        {
            if (CarePackageStores.Pick(stores, _careRng.Next) is not { } store
                || !_carePackages.PlaceDeferred(store.Id))
            {
                break;
            }

            granted = true;
            _log.LogInfo(
                $"care package granted at {store.Name} " +
                $"({_carePackages.Total}/{CarePackageLedger.MaxPackages})");
        }

        if (granted)
        {
            _saveDriver();
        }

        if (_carePackages.Unplaced > 0)
        {
            _log.LogWarning(
                $"care package not granted yet for {_carePackages.Unplaced} kit(s): " +
                "no store door in town");
        }
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
            _wallet.ClubReputation,
            _wallet.BankBalance,
            _jobs.TaxiCompleted,
            _jobs.DeliveryCompleted,
            // WHY the settled total: with no GPS in the port every drive is a
            // drive without GPS, so each settled job is one destination reached
            // (Unity's manual pins measured a map action this port has none of).
            _jobs.Completed,
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
