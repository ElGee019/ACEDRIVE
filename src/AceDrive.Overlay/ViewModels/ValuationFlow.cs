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
/// panel, and the companion's ENTER button (voice included) opens the showcase
/// once the car is parked at the venue. The player enters one car, its score is
/// fixed for the week, and every later entrant on the shared list is compared
/// against it — a lower score pays the winner's gap/500, a higher one the flat
/// consolation a lost showcase pays (never a share of the gap). The list
/// resets on the Sunday-to-Monday midnight crossing; a restart inside the same
/// week catches up on the wins that landed while the overlay was closed.
///
/// The entry itself lives two real-world days, not the whole week: the window
/// exists so an idle driver cannot keep banking comparisons without playing.
/// When it closes the flow settles the newcomers who turned up inside it,
/// deletes the own doc and forgets the entry, so rivals stop being compared
/// against a car nobody is showcasing any more. Entering next week's car
/// overwrites the old entry the same way, through the weekly reset.
///
/// Location selection is a placeholder until the Eifel map can resolve real
/// addresses: <see cref="ValuationRules.PickVenue"/> draws a random house on
/// the operating zone's streets (TODO at that helper). The same zone map
/// resolves a saved venue, so the showcase can never sit on a street the zone
/// has not discovered (TOZONES §4.7).
/// </summary>
public sealed class ValuationFlow
{
    private const double ResultSeconds = 4.0;
    private const double CatchUpRetrySeconds = 60.0;

    private readonly OperatingZone _zone;
    private readonly GameClock _game;
    private readonly IClock _clock;
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
    // The in-flight catch-up read belongs to an expired entry: its answer
    // settles the two-day window and then forgets the entry instead of
    // reopening the week's comparison.
    private bool _catchingUpExpiry;
    private DateTime _nextCatchUpAt = DateTime.MinValue;
    private readonly ConcurrentQueue<IReadOnlyList<ValuationEntry>> _snapshots = new();
    private readonly Queue<PendingResult> _results = new();
    private DateTime _nextResultAt = DateTime.MinValue;
    private ValuationEntry? _houseEntry;

    // TEMPORARY (testing): true skips the Monday-window and one-entry-a-week
    // gates, so the showcase can be driven and entered again at any time. The
    // app passes true; the tests keep the shipping reading.
    private readonly bool _ignoreEventGates;

    public ValuationFlow(
        OperatingZone zone, GameClock game, IClock clock, ValuationLedger ledger,
        IValuationBoard board, Garage garage, IReadOnlyList<CarSpec> catalog,
        DriverWallet wallet, IProfileStore profiles, ITelemetryHud? hud,
        GameStateSaver saver, IRivalsService rivals, VoiceBeat beat,
        Random rng, ILog log, bool ignoreEventGates = false)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(clock);
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

        _zone = zone;
        _game = game;
        _clock = clock;
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
        _ignoreEventGates = ignoreEventGates;
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
        if (!_ignoreEventGates
            && !ValuationRules.IsWindow(ControlCalendar.ToDayOfWeek(now), now.Hour))
        {
            _log.LogInfo("market valuation drive ignored: outside the Monday 8am-8pm window");
            return;
        }
        // One entry a week (Unity's lastValuationGameDay gate): a row whose
        // showcase was already entered cannot send the player out again.
        if (_screen is not null
            || (!_ignoreEventGates && _ledger.HasEntry(ValuationRules.WeekKey(now))))
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
    /// draw, the two-day expiry, the offline catch-up, the board snapshots and
    /// the queued result notices. No disk or network call runs unless a
    /// transition needs it.
    /// <paramref name="now"/> is the wall clock (the entry's expiry, the 4 s
    /// notice cadence and the catch-up retry); every calendar decision reads the
    /// game clock instead, so a wall hour can never open or close the Monday
    /// window.
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

        // The two-day cap comes before the catch-up: an entry whose window
        // closed must not reopen the week's comparison. It settles what landed
        // inside the window, then forgets the entry.
        if (_ledger.HasEntry(week) && _ledger.IsExpired(now))
        {
            TickExpiredEntry(now);
        }
        else
        {
            TryCatchUp(now, week);
        }
        DrainSnapshots();
        DrainDraw();
        DrainCatchUp(now);
        ShowNextResult(now);
    }

    /// <summary>
    /// ENTER on the pinned venue (reached through <see cref="ValuationPoiHandler"/>;
    /// the companion's button, voice included, raises it): the arrival consumes
    /// the venue and hands back the showcase page. A park alone only puts the
    /// button up — the player decides when the showcase opens.
    /// </summary>
    public object? EnterVenue(PointOfInterest? poi)
    {
        if (_screen is not null)
        {
            return _screen;
        }
        if (poi is null || !_ledger.Driving)
        {
            return null;
        }
        if (!TryVenue(out ValuationVenue venue))
        {
            _ledger.EndDrive();
            return null;
        }

        _ledger.CompleteEvent();
        _hud?.SetDestination(null);
        _saver.SaveDriver(_rivals.LastResult);
        _screen = CreateScreen(venue);
        _log.LogInfo(
            $"market valuation reached {AddressFormatter.Format(venue.Location)}: " +
            $"showcase open ({_ledger.EventsCompleted} event(s) completed)");
        return _screen;
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
        // TODO(leaderboards): replace the per-car placeholder with the rank the
        // stretch boards report for this model (ValuationRules.LeaderboardRanking)
        // once they exist; until then every owned car carries the top percentile.
        double ranking = owned?.LeaderboardRanking ?? 0.0;
        double score = ValuationRules.Score(spec, jobs, broken, ranking);
        int week = ValuationRules.WeekKey(_game.GameNow);
        string name = PlayerName();
        _ledger.MarkEntry(carKey, score, week, _clock.UtcNow);
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
            $"{jobs} job(s), engine {broken:P0} broken, " +
            $"leaderboard {ranking:0.00} (+{ValuationRules.LeaderboardPoints(ranking):0.0})");
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
        // The arrival is the button: parked at the venue the companion invites
        // ENTER (and the same word by voice) instead of telling the player to
        // park at a house the car is already standing on.
        _hud?.SetDestination(
            poi,
            new HudDestinationCopy(
                "HudValuationDrive", ArrivedKey: string.Empty, ActionKey: "HudEnter"));
        _hud?.ShowTaskText();
    }

    private ValuationVenue? DrawVenue()
    {
        ValuationVenue? venue = ValuationRules.PickVenue(_zone.ZoneMap, _rng.Next);
        if (venue is null)
        {
            _log.LogWarning("market valuation venue not drawn: no house in the operating zone");
            return null;
        }
        _ledger.SetVenue(venue);
        _saver.SaveDriver(_rivals.LastResult);
        _log.LogInfo(
            $"market valuation venue drawn: {AddressFormatter.Format(venue.Location)}");
        return venue;
    }

    // The stored venue resolved against the zone's live map: the draw and the
    // resolve read one owner, so a saved door can never sit off-zone. A stale
    // street/house (an old save, a shrunk zone) consumes the venue instead of
    // throwing; the next 6am preload draws a fresh one.
    private bool TryVenue(out ValuationVenue venue)
    {
        venue = null!;
        if (_ledger.VenueStreetId is not { Length: > 0 } streetId)
        {
            return false;
        }
        AddressMap zoneMap = _zone.ZoneMap;
        AddressStreet? street = AddressBook.Street(zoneMap, streetId);
        if (street is null
            || _ledger.VenueHouseNumber < 1
            || _ledger.VenueHouseNumber > HouseNumberPlan.Count(street, zoneMap))
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

        // Nobody owns the house car, so it carries no job, wear or leaderboard
        // credit: its score is the catalogue formula alone, the bar the player's
        // entry has to clear when no rival is listed.
        double score = ValuationRules.Score(
            _catalog.GetValueOrDefault(key), 0, 0.0, leaderboardRanking: 0.0);
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
    // re-seeds silently and stops comparing. A closed window pays nothing: the
    // snapshot may still be in flight from the watcher when the cap lands.
    private void ProcessSnapshot(IReadOnlyList<ValuationEntry> entries)
    {
        string? ownUid = _board.OwnUid;
        if (string.IsNullOrEmpty(ownUid) || _ledger.EntryWeek != ValuationRules.WeekKey(_game.GameNow)
            || _ledger.IsExpired(_clock.UtcNow))
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
            // Ties are Unity's ignored case — no popup, no payout — even
            // though the showcase page reads a tie as a win.
            if (entry.Score == own.Score)
            {
                continue;
            }
            // One rule for every payout: the page, this listener and the
            // restart catch-up all read ValuationRules.Verdict, so a win can
            // never pay a different amount depending on where it landed.
            ValuationVerdict verdict = ValuationRules.Verdict(own.Score, entry.Score);
            _results.Enqueue(new PendingResult(
                entry.PlayerName, verdict.Xp, verdict.Won, Offline: false, WinCount: 1));
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
        bool settlesExpiry = _catchingUpExpiry;
        _catchingUpExpiry = false;
        IReadOnlyList<ValuationEntry>? entries = ResultOf(task);
        if (entries is null)
        {
            _nextCatchUpAt = now + TimeSpan.FromSeconds(CatchUpRetrySeconds);
            return;
        }
        if (settlesExpiry)
        {
            SettleExpiredEntry(entries);
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
        // The refresh keeps the entry's own stamp: a catch-up read must not
        // restart the two-day window it is settling against.
        _ledger.MarkEntry(
            own.CarKey, own.Score, _ledger.EntryWeek,
            _ledger.EnteredAtUtc ?? _clock.UtcNow);
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

    // The two-day cap's tick. A session that was live while the window closed
    // already paid every comparison as it landed, so forgetting is the whole
    // job; a session that opens after the window first settles the newcomers
    // that arrived inside it. Offline keeps the entry (there is no list to
    // settle against) and retries on a later tick.
    private void TickExpiredEntry(DateTime now)
    {
        if (_catchUpDone)
        {
            ForgetEntry("two-day window closed");
            return;
        }
        if (!_board.IsOnline || _catchUpStarted
            || string.IsNullOrEmpty(_board.OwnUid) || now < _nextCatchUpAt)
        {
            return;
        }

        _catchUpStarted = true;
        _catchingUpExpiry = true;
        _catchUpTask = _board.ListAsync();
    }

    // The window's last word: pay the wins that landed inside it, then forget
    // the entry. The own doc may already be gone (the Firestore TTL fires on
    // the same 48 h), so a missing own entry is not an error here — the window
    // is measured from the local stamp, not from the doc that just died.
    private void SettleExpiredEntry(IReadOnlyList<ValuationEntry> entries)
    {
        DateTime expiresAt = _ledger.ExpiresAtUtc ?? _clock.UtcNow;
        ValuationOfflineGains gains = ValuationRules.ComputeOfflineGains(
            _ledger.OwnScore, _ledger.SeenUids, entries, _board.OwnUid, expiresAt);
        _ledger.RecordSeen(gains.NewSeenUids);
        if (gains.WinCount > 0 && gains.TotalXp > 0.0)
        {
            _results.Enqueue(new PendingResult(
                string.Empty, gains.TotalXp, Won: true, Offline: true, gains.WinCount));
        }
        ForgetEntry(
            $"two-day window closed ({gains.WinCount} newcomer(s) settled inside it)");
    }

    // Forgetting is the entry leaving both the game and the shared list: the
    // player's own doc goes (a rival's never does), the watcher stops, and the
    // settled set stays so a later entry in the same week cannot pay a
    // newcomer this one already settled.
    private void ForgetEntry(string why)
    {
        _board.StopWatching();
        _ledger.ClearEntry();
        _saver.SaveDriver(_rivals.LastResult);
        SafeTask.Forget(_board.WipeOwnAsync(), _log, "valuation expiry wipe");
        _log.LogInfo($"market valuation entry forgotten: {why}");
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
                : Strings.Format("ValuationNoticeLoss", result.PlayerName, result.Xp);
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
