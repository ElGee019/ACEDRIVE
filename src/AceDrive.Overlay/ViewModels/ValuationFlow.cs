using System.Collections.Concurrent;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The weekly market valuation (PORTPLAN item 49, ports Unity ValuationService,
/// ValuationHandler, ValuationScreen's workflow and ValuationPopScreen's
/// newcomer results).
///
/// The week is Unity's: the venue is drawn on Monday from 6am, the calendar's
/// MARKET VALUATION row (Monday 8am-8pm) pins it on the companion and hides the
/// panel, and parking at the venue opens the showcase automatically. The player
/// enters one car, its score is fixed for the week, and every later entrant on
/// the shared list is compared against it — a lower score pays the winner's
/// gap/500, a higher one the loser's gap/1000 consolation. The list resets on
/// the Sunday-to-Monday midnight crossing; a restart inside the same week
/// catches up on the wins that landed while the overlay was closed.
///
/// Location selection is a placeholder until the Eifel map can resolve real
/// addresses: <see cref="ValuationRules.PickVenue"/> draws a random house on
/// the two placeholder streets (TODO at that helper).
/// </summary>
public sealed class ValuationFlow
{
    private const double ResultSeconds = 4.0;
    private const double CatchUpRetrySeconds = 60.0;

    private readonly AddressMap _map;
    private readonly GameClock _game;
    private readonly ValuationLedger _ledger;
    private readonly IValuationBoard _board;
    private readonly Garage _garage;
    private readonly IReadOnlyDictionary<string, CarSpec> _catalog;
    private readonly DriverWallet _wallet;
    private readonly IProfileStore _profiles;
    private readonly ITelemetryHud? _hud;
    private readonly GameStateSaver _saver;
    private readonly IRivalsService _rivals;
    private readonly VoiceBeat _beat;
    private readonly Random _rng;
    private readonly ILog _log;

    // WHY late: the host is the coordinator, which is built after this flow.
    // The attach closes in the coordinator's constructor, before any calendar
    // tap or arrival can reach it.
    private IValuationHost _host = null!;

    private ValuationScreenViewModel? _screen;
    private Task<IReadOnlyList<ValuationEntry>?>? _drawTask;
    private Task<IReadOnlyList<ValuationEntry>?>? _catchUpTask;
    private bool _catchUpDone;
    private bool _catchUpStarted;
    private DateTime _nextCatchUpAt = DateTime.MinValue;
    private readonly ConcurrentQueue<IReadOnlyList<ValuationEntry>> _snapshots = new();
    private readonly Queue<PendingResult> _results = new();
    private DateTime _nextResultAt = DateTime.MinValue;
    private ValuationEntry? _houseEntry;

    public ValuationFlow(
        AddressMap map, GameClock game, ValuationLedger ledger,
        IValuationBoard board, Garage garage, IReadOnlyList<CarSpec> catalog,
        DriverWallet wallet, IProfileStore profiles, ITelemetryHud? hud,
        GameStateSaver saver, IRivalsService rivals, VoiceBeat beat,
        Random rng, ILog log)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(garage);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(saver);
        ArgumentNullException.ThrowIfNull(rivals);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(log);

        _map = map;
        _game = game;
        _ledger = ledger;
        _board = board;
        _garage = garage;
        var byKey = new Dictionary<string, CarSpec>(StringComparer.Ordinal);
        foreach (CarSpec car in catalog)
        {
            byKey.TryAdd(car.Key, car);
        }
        _catalog = byKey;
        _wallet = wallet;
        _profiles = profiles;
        _hud = hud;
        _saver = saver;
        _rivals = rivals;
        _beat = beat;
        _rng = rng;
        _log = log;
        _board.Changed += OnBoardChanged;
    }

    /// <summary>Attaches the coordinator that owns the panel and the page policy.</summary>
    public void Attach(IValuationHost host)
    {
        _host = host;
        // Offline arrival (Unity's valuationDriving resume): the leg was
        // already in flight when the process died, so the venue is pinned
        // again and the next park still opens the showcase.
        if (_ledger.Driving && TryVenue(out ValuationVenue venue))
        {
            PinVenue(venue);
        }
        else if (_ledger.Driving)
        {
            _ledger.EndDrive();
        }
    }

    /// <summary>
    /// The calendar row's tap (Unity's job-centre button): pins the venue and
    /// sends the player off. Off-window taps are refused here as well as by the
    /// calendar page, so a caller can never jump the gate.
    /// </summary>
    public void BeginDrive()
    {
        DateTime now = _game.GameNow;
        if (!ValuationRules.IsWindow(ControlCalendar.ToDayOfWeek(now), now.Hour))
        {
            _log.LogInfo("market valuation drive ignored: outside the Monday 8am-8pm window");
            return;
        }
        // One entry a week (Unity's lastValuationGameDay gate): a row whose
        // showcase was already entered cannot send the player out again.
        if (_screen is not null || _ledger.HasEntry(ValuationRules.WeekKey(now)))
        {
            return;
        }

        if (!TryVenue(out ValuationVenue venue))
        {
            if (DrawVenue() is not { } drawn)
            {
                return;
            }
            venue = drawn;
        }

        PinVenue(venue);
        _ledger.BeginDrive();
        _saver.SaveDriver(_rivals.LastResult);
        _host.HidePanel();
        _log.LogInfo(
            $"market valuation: driving to {AddressFormatter.Format(venue.Location)}");
    }

    /// <summary>
    /// One poll of the weekly bookkeeping: the Monday reset, the 6am venue
    /// draw, the offline catch-up, the board snapshots and the queued result
    /// notices. No disk or network call runs unless a transition needs it.
    /// <paramref name="now"/> is the wall clock (the 4 s notice cadence and the
    /// catch-up retry); every calendar decision reads the game clock instead,
    /// so a wall hour can never open or close the Monday window.
    /// </summary>
    public void Tick(DateTime now)
    {
        DateTime gameNow = _game.GameNow;
        int week = ValuationRules.WeekKey(gameNow);
        if (_ledger.LastResetWeek < week)
        {
            ResetWeek(week);
        }

        // The showcase already being open is not a reason to draw another
        // venue: the arrival consumed this week's one, and only the player's
        // way out of the page may put a retry on the calendar.
        if (_screen is null && !_ledger.HasVenue && !_ledger.Driving
            && !_ledger.HasEntry(week)
            && ValuationRules.IsPreloadWindow(
                ControlCalendar.ToDayOfWeek(gameNow), gameNow.Hour))
        {
            DrawVenue();
        }

        TryCatchUp(now, week);
        DrainSnapshots();
        DrainDraw();
        DrainCatchUp(now);
        ShowNextResult(now);
    }

    /// <summary>
    /// One poll while the drive is live: parking at the pinned venue completes
    /// the leg and reveals the showcase (Unity's OnDestinationReached).
    /// </summary>
    public void Advance(TelemetrySample? sample)
    {
        if (sample is null || !_ledger.Driving || _hud is null || !_hud.ParkedAtTarget)
        {
            return;
        }

        OnArrival();
    }

    /// <summary>The entry phase's ENTER: records the car and draws an opponent.</summary>
    public void Enter(string carKey)
    {
        if (_screen is null || string.IsNullOrWhiteSpace(carKey))
        {
            return;
        }

        OwnedCar? owned = _garage.Find(carKey);
        CarSpec? spec = owned is not null ? _garage.SpecOf(owned) : _catalog.GetValueOrDefault(carKey);
        double broken = owned?.Engine.BrokenPercent(
            spec?.EngineCc ?? 0, owned.Odometer) ?? 0.0;
        int jobs = owned?.JobsCompleted ?? 0;
        double score = ValuationRules.Score(spec, jobs, broken);
        int week = ValuationRules.WeekKey(_game.GameNow);
        string name = PlayerName();
        _ledger.MarkEntry(carKey, score, week);
        _catchUpDone = true;
        _saver.SaveDriver(_rivals.LastResult);
        _screen.ShowWaiting(name, carKey, score);
        SafeTask.Forget(
            _board.PushEntryAsync(name, carKey, jobs, broken, score),
            _log, "valuation entry push");
        _drawTask = _board.ListAsync();
        _board.StartWatching();
        _log.LogInfo(
            $"market valuation entered: {carKey}, score {score:0.00}, " +
            $"{jobs} job(s), engine {broken:P0} broken");
    }

    /// <summary>CONTINUE: pays the verdict XP and returns to the control page.</summary>
    public void Continue(double xp)
    {
        if (xp > 0.0)
        {
            _wallet.AddXp(xp);
            _saver.SaveDriver(_rivals.LastResult);
            _log.LogInfo($"market valuation verdict paid: {xp:0.000} XP");
        }

        _screen = null;
        _host.ShowControl();
    }

    /// <summary>BACK on the entry phase: leaves without entering.</summary>
    public void Cancel()
    {
        _screen = null;
        if (!_ledger.HasEntry(ValuationRules.WeekKey(_game.GameNow)))
        {
            _board.StopWatching();
        }
        _host.ShowControl();
        _log.LogInfo("market valuation closed without an entry");
    }

    // The weekly reset: Unity's Monday-midnight wipe. The local entry, its
    // seen set and the venue go, the own remote doc is deleted (never a
    // rival's), and the new week is stamped so the reset runs once.
    private void ResetWeek(int week)
    {
        bool hadEntry = _ledger.EntryWeek != ValuationLedger.NoWeek;
        _board.StopWatching();
        _ledger.MarkReset(week);
        _houseEntry = null;
        _catchUpDone = false;
        _catchUpStarted = false;
        _catchUpTask = null;
        _drawTask = null;
        // Last week's unshown results belong to the wiped list: paying one
        // after the reset would bank XP for an entry nobody can see any more.
        _results.Clear();
        _saver.SaveDriver(_rivals.LastResult);
        if (hadEntry)
        {
            SafeTask.Forget(_board.WipeOwnAsync(), _log, "valuation weekly wipe");
        }
        // A showcase still on screen has no week behind it any more; hand the
        // player back instead of leaving a page whose verbs answer nothing.
        if (_screen is not null)
        {
            _screen = null;
            _host.ShowControl();
        }
        _log.LogInfo($"market valuation reset: week {week}");
    }

    // The entry phase's arrival. The venue is consumed before the page opens,
    // exactly like Unity's arrival tail: the week's street cannot be reused.
    private void OnArrival()
    {
        if (!TryVenue(out ValuationVenue venue))
        {
            _ledger.EndDrive();
            return;
        }

        _ledger.CompleteEvent();
        _hud?.SetDestination(null);
        _saver.SaveDriver(_rivals.LastResult);
        _screen = CreateScreen(venue);
        _host.ShowValuation(_screen);
        _log.LogInfo(
            $"market valuation reached {AddressFormatter.Format(venue.Location)}: " +
            $"showcase open ({_ledger.EventsCompleted} event(s) completed)");
    }

    private ValuationScreenViewModel CreateScreen(ValuationVenue venue) =>
        new(
            _garage, _catalog,
            Strings.Format("ValuationVenueLine", AddressFormatter.Format(venue.Location)),
            Enter, Continue, new RelayCommand(Cancel), _beat);

    private void PinVenue(ValuationVenue venue)
    {
        PointOfInterest poi = new(
            "valuation", Strings.Get("EventValuation"), venue.Location,
            PoiTypes.Valuation);
        _hud?.SetDestination(
            poi,
            new HudDestinationCopy(
                "HudValuationDrive", "HudValuationArrived", ActionKey: string.Empty));
        _hud?.ShowTaskText();
    }

    private ValuationVenue? DrawVenue()
    {
        ValuationVenue? venue = ValuationRules.PickVenue(_map, _rng.Next);
        if (venue is null)
        {
            _log.LogWarning("market valuation venue not drawn: no house on the placeholder map");
            return null;
        }
        _ledger.SetVenue(venue);
        _saver.SaveDriver(_rivals.LastResult);
        _log.LogInfo(
            $"market valuation venue drawn: {AddressFormatter.Format(venue.Location)}");
        return venue;
    }

    // The stored venue resolved against the live map. A stale street/house
    // (hand-edited map, old save) consumes the venue instead of throwing.
    private bool TryVenue(out ValuationVenue venue)
    {
        venue = null!;
        if (_ledger.VenueStreetId is not { Length: > 0 } streetId)
        {
            return false;
        }
        AddressStreet? street = AddressBook.Street(_map, streetId);
        if (street is null
            || _ledger.VenueHouseNumber < 1
            || _ledger.VenueHouseNumber > HouseNumberPlan.Count(street, _map))
        {
            _ledger.ClearVenue();
            return false;
        }
        venue = new ValuationVenue(street.Id, street.Name, _ledger.VenueHouseNumber);
        return true;
    }

    private void DrainDraw()
    {
        if (_drawTask is not { IsCompleted: true } task)
        {
            return;
        }
        _drawTask = null;
        IReadOnlyList<ValuationEntry>? entries = ResultOf(task);
        if (_ledger.EntryCarKey is not { Length: > 0 } ownCar)
        {
            return;
        }

        ValuationEntry competitor =
            ValuationRules.SelectCompetitor(entries, _board.OwnUid, _rng.Next)
            ?? HouseEntry()
            ?? new ValuationEntry(
                ValuationRules.HouseEntryDocId, ValuationRules.HouseEntryName,
                ownCar, 0, 0.0, _ledger.OwnScore);
        if (entries is not null)
        {
            _ledger.RecordSeen(Uids(entries));
            _saver.SaveDriver(_rivals.LastResult);
        }
        ValuationVerdict verdict = ValuationRules.Verdict(_ledger.OwnScore, competitor.Score);
        _screen?.ShowCompetitor(competitor, verdict);
    }

    // The competitor list's house fallback (Unity UseHouseFallback): a fresh
    // random catalogue car, drawn locally and compared with score only.
    private ValuationEntry? HouseEntry()
    {
        if (_houseEntry is not null)
        {
            return _houseEntry;
        }
        if (_catalog.Count == 0)
        {
            return null;
        }

        List<string> keys = [.. _catalog.Keys];
        string? key = ValuationRules.PickRandomCarId(keys, _rng.Next);
        if (key is null)
        {
            return null;
        }
        double score = ValuationRules.Score(_catalog.GetValueOrDefault(key), 0, 0.0);
        _houseEntry = new ValuationEntry(
            ValuationRules.HouseEntryDocId, ValuationRules.HouseEntryName,
            key, 0, 0.0, score);
        return _houseEntry;
    }

    private void OnBoardChanged(IReadOnlyList<ValuationEntry> entries) =>
        _snapshots.Enqueue(entries);

    private void DrainSnapshots()
    {
        while (_snapshots.TryDequeue(out IReadOnlyList<ValuationEntry>? entries))
        {
            if (entries is not null)
            {
                ProcessSnapshot(entries);
            }
        }
    }

    // A live snapshot from the watcher: every newcomer pays a win or a
    // consolation, exactly like Unity's listener. A missing own entry means the
    // player is not on this week's list any more (wipe/TTL), so the flow
    // re-seeds silently and stops comparing.
    private void ProcessSnapshot(IReadOnlyList<ValuationEntry> entries)
    {
        string? ownUid = _board.OwnUid;
        if (string.IsNullOrEmpty(ownUid) || _ledger.EntryWeek != ValuationRules.WeekKey(_game.GameNow))
        {
            return;
        }

        ValuationEntry? own = FindOwn(entries, ownUid);
        if (own is null)
        {
            _ledger.RecordSeen(Uids(entries));
            _ledger.ClearEntry();
            _board.StopWatching();
            _catchUpDone = true;
            _saver.SaveDriver(_rivals.LastResult);
            _log.LogInfo("market valuation entry missing from the weekly list; settled set reseeded");
            return;
        }

        IReadOnlyList<ValuationEntry> newcomers =
            ValuationRules.FindUnseen(_ledger.SeenUids, entries, ownUid);
        foreach (ValuationEntry entry in newcomers)
        {
            if (entry.Score < own.Score)
            {
                _results.Enqueue(new PendingResult(
                    entry.PlayerName, (own.Score - entry.Score) / ValuationRules.WinXpDivisor,
                    Won: true, Offline: false, WinCount: 1));
            }
            else if (entry.Score > own.Score)
            {
                _results.Enqueue(new PendingResult(
                    entry.PlayerName, (entry.Score - own.Score) / ValuationRules.LossXpDivisor,
                    Won: false, Offline: false, WinCount: 1));
            }
            // Ties are Unity's ignored case: no popup, no payout.
        }

        _ledger.RecordSeen(Uids(entries));
        _saver.SaveDriver(_rivals.LastResult);
        if (newcomers.Count > 0)
        {
            _log.LogInfo($"market valuation: {newcomers.Count} newcomer(s) compared");
        }
    }

    // The restart catch-up: one list read when the saved week still has an
    // entry. Wins only (Unity's offline contract); a missing own doc re-seeds.
    private void TryCatchUp(DateTime now, int week)
    {
        if (_catchUpDone || !_board.IsOnline || _ledger.EntryWeek != week)
        {
            return;
        }
        if (string.IsNullOrEmpty(_board.OwnUid) || now < _nextCatchUpAt)
        {
            return;
        }
        if (_catchUpStarted)
        {
            return;
        }

        _catchUpStarted = true;
        _catchUpTask = _board.ListAsync();
    }

    private void DrainCatchUp(DateTime now)
    {
        if (_catchUpTask is not { IsCompleted: true } task)
        {
            return;
        }
        _catchUpTask = null;
        _catchUpStarted = false;
        IReadOnlyList<ValuationEntry>? entries = ResultOf(task);
        if (entries is null)
        {
            _nextCatchUpAt = now + TimeSpan.FromSeconds(CatchUpRetrySeconds);
            return;
        }

        string? ownUid = _board.OwnUid;
        ValuationEntry? own = FindOwn(entries, ownUid);
        if (own is null)
        {
            _ledger.RecordSeen(Uids(entries));
            _ledger.ClearEntry();
            _board.StopWatching();
            _catchUpDone = true;
            _saver.SaveDriver(_rivals.LastResult);
            _log.LogInfo("market valuation catch-up: own entry gone; settled set reseeded");
            return;
        }

        ValuationOfflineGains gains = ValuationRules.ComputeOfflineGains(
            _ledger.OwnScore, _ledger.SeenUids, entries, ownUid);
        _ledger.RecordSeen(gains.NewSeenUids);
        _ledger.MarkEntry(own.CarKey, own.Score, _ledger.EntryWeek);
        _catchUpDone = true;
        _board.StartWatching();
        _saver.SaveDriver(_rivals.LastResult);
        if (gains.WinCount > 0 && gains.TotalXp > 0.0)
        {
            _results.Enqueue(new PendingResult(
                string.Empty, gains.TotalXp, Won: true, Offline: true, gains.WinCount));
            _log.LogInfo(
                $"market valuation catch-up: {gains.WinCount} win(s), {gains.TotalXp:0.000} XP");
        }
        else
        {
            _log.LogInfo("market valuation catch-up: no wins while away");
        }
    }

    private void ShowNextResult(DateTime now)
    {
        if (_results.Count == 0 || now < _nextResultAt)
        {
            return;
        }

        PendingResult result = _results.Dequeue();
        _nextResultAt = now + TimeSpan.FromSeconds(ResultSeconds);
        if (result.Xp > 0.0)
        {
            _wallet.AddXp(result.Xp);
            _saver.SaveDriver(_rivals.LastResult);
        }

        string text = result.Offline
            ? Strings.Format("ValuationOfflineSummary", result.WinCount, result.Xp)
            : result.Won
                ? Strings.Format("ValuationNoticeWin", result.PlayerName, result.Xp)
                : Strings.Format("ValuationNoticeLoss", result.PlayerName);
        _hud?.ShowNotice(text, TimeSpan.FromSeconds(ResultSeconds));
        _log.LogInfo($"market valuation result: {(result.Won ? "won" : "lost")} " +
            $"{result.Xp:0.000} XP{(result.Offline ? " (while away)" : "")}");
    }

    private static ValuationEntry? FindOwn(IReadOnlyList<ValuationEntry> entries, string? ownUid)
    {
        if (string.IsNullOrEmpty(ownUid))
        {
            return null;
        }
        foreach (ValuationEntry entry in entries)
        {
            if (string.Equals(entry.Uid, ownUid, StringComparison.Ordinal))
            {
                return entry;
            }
        }
        return null;
    }

    private static List<string> Uids(IReadOnlyList<ValuationEntry> entries)
    {
        List<string> uids = new(entries.Count);
        foreach (ValuationEntry entry in entries)
        {
            if (!string.IsNullOrEmpty(entry.Uid))
            {
                uids.Add(entry.Uid);
            }
        }
        return uids;
    }

    private static IReadOnlyList<ValuationEntry>? ResultOf(
        Task<IReadOnlyList<ValuationEntry>?> task) =>
        task.Status == TaskStatus.RanToCompletion ? task.Result : null;

    private string PlayerName() =>
        _profiles.Load()?.DisplayName is { Length: > 0 } name
            ? name
            : Strings.Get("ValuationYou");

    private sealed record PendingResult(
        string PlayerName, double Xp, bool Won, bool Offline, int WinCount);
}
