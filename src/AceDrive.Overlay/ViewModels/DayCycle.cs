using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The session's calendar: the game-day flip, the display week it rolls on the
/// Sunday-midnight crossing, and the idle shift timeout (Unity's 30-minute
/// rule) with the shift-ended page it opens.
///
/// WHY it owns the week: the day flip is what moves it, so the pages that stamp
/// or draw a week read it from here (<see cref="Navigation"/>, the dealer floor)
/// instead of keeping a mirror of the coordinator's field. The poll only
/// forwards the tick; the guards (the day compare, the second-guarded shift
/// countdown) live here, so a parked session costs one int compare per tick
/// (AGENTS.md hot-path discipline).
/// </summary>
public sealed class DayCycle
{
    private readonly GameClock _game;
    private readonly TaxiShift _shift;
    private readonly ShiftRecord _records;
    private readonly IShiftStandings _standings;
    private readonly IPlayerTelemetry _online;
    // The driver name behind the shift push: the profile file is the one the
    // story flow banks when the opening screen is confirmed, and a shift end is
    // far too rare for the extra read to matter.
    private readonly IProfileStore _profiles;
    // The board snapshot the driver save carries rides along from the last
    // refresh; the shift end itself only contributes its own record.
    private readonly IRivalsService _rivals;
    private readonly GameStateSaver _saver;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;
    // What the calendar can still open: the weekday/week/window rule joined to
    // the event ledgers' used-up stamps.
    private readonly CalendarGate _gate;

    // The day and hour the session opened on: the first poll must not report a
    // flip. The hour joins the guard because the event gate opens and closes on
    // the 8am/8pm edges; every tick in between is two int compares.
    private int _lastDay;
    private int _lastHour;

    // The today-event gate the menu's calendar tile shows. Watched here because
    // the calendar's day and week are this cycle's: the flip is pushed once,
    // never re-derived by the page on every tick.
    private bool _eventPlayable;

    // The second the shift readout was last painted, so an open-but-idle shift
    // costs one format per second instead of one per 10 Hz tick.
    private int _lastShiftSecs = -1;

    // WHY late: the host is the coordinator, which is built after this cycle.
    // Every member below is only reachable from a tick or a shift end, and the
    // coordinator attaches in its constructor before anything can fire.
    private IDayCycleHost _host = null!;

    public DayCycle(
        GameClock game, TaxiShift shift, ShiftRecord records,
        IShiftStandings standings, IPlayerTelemetry online, IProfileStore profiles,
        IRivalsService rivals, GameStateSaver saver, VoiceBeat beat,
        CalendarGate gate, int startWeek, ILog log)
    {
        _game = game;
        _shift = shift;
        _records = records;
        _standings = standings;
        _online = online;
        _profiles = profiles;
        _rivals = rivals;
        _saver = saver;
        _beat = beat;
        _log = log;
        _gate = gate;
        DisplayWeek = startWeek;
        DateTime gameNow = game.GameNow;
        _lastDay = ControlCalendar.ToDayOfWeek(gameNow);
        _lastHour = gameNow.Hour;
        // Seeded from the clock so a page built before the first tick already
        // wears the artwork the gate calls for.
        _eventPlayable = gate.OpenEventNow(gameNow, startWeek) is not null;
    }

    /// <summary>
    /// Display week: rolls on the Sunday-midnight crossing and persists across
    /// restarts through the clock store (seeded from the save at startup).
    /// </summary>
    public int DisplayWeek { get; private set; }

    /// <summary>
    /// Whether today's calendar event is inside its 8am-8pm gate: the value the
    /// cached menu's calendar tile mirrors. A page built between two flips reads
    /// it here (<see cref="Navigation.MenuPage"/>); a page already open hears
    /// the flip through <see cref="IDayCycleHost.EventGateChanged"/>.
    /// </summary>
    public bool EventPlayable => _eventPlayable;

    /// <summary>Attaches the coordinator that reveals the pages and paints the readouts.</summary>
    public void Attach(IDayCycleHost host) => _host = host;

    /// <summary>
    /// The session's timed transitions, once per poll: the idle shift timeout
    /// (Unity's 30-minute rule), so the taxi button flips back even when the
    /// player never taps it, the game-day flip with the week it completes, the
    /// event gate the menu's calendar tile mirrors, and the second-guarded shift
    /// countdown. Both job reads arrive as values: an accepted job is work in
    /// progress and must not idle the shift out mid-ride, and a live leg already
    /// repaints the readouts every tick. Rides the wall clock, so a closed game
    /// cannot freeze a shift or a day.
    /// </summary>
    public void Tick(DateTime now, bool jobInProgress, bool legActive)
    {
        if (!jobInProgress && _shift.IsTimedOut(now) && _shift.TryEndTimedOut(now, out TimeSpan idle))
        {
            EndShift(idle);
        }

        // The calendar reads the clock once for its day and hour compares: the
        // day flip it reports and the event gate it watches.
        TickCalendar(_game.GameNow);
        TickShiftCountdown(now, legActive);
    }

    /// <summary>
    /// Ends a shift that already timed out (Unity chains the stats page before
    /// the map, so a shift is never silently over): banks the closed state, the
    /// lifetime record and the world push, then opens the page. Both timeout
    /// sites and the stale tap land here. The page's CONTINUE hands the player
    /// back to whatever the timeout interrupted, so the report never costs
    /// them their place.
    /// </summary>
    public void EndShift(TimeSpan idle)
    {
        ShiftSummary summary = _shift.Summary();
        _log.LogInfo($"shift ended (timeout after {idle.TotalMinutes:0}m idle): " +
            $"{summary.Jobs} jobs / {summary.Earnings:0.00}");
        _lastShiftSecs = -1;
        // The closed state goes to disk before the record push: a shift that
        // timed out between sessions is already over and must never reopen.
        _saver.SaveShift();
        // The record is stamped when the idle window closed, not when the
        // process noticed: an expiry discovered at the next launch can be days
        // later, and the world board breaks earnings ties by this stamp.
        DateTime endedAt = _shift.LastJobEnd + TimeSpan.FromMinutes(TaxiShift.TimeoutMinutes);
        _records.Complete(summary.Jobs, summary.Earnings, endedAt.Ticks);
        // Refresh before the page reads it: the page then answers with the
        // fresh rank or the restored snapshot, and follows the board's event if
        // it is still loading.
        if (_standings.Refresh(_records.BestEarnings, _records.BestFirstEarnedTicks) is { } standings)
        {
            _records.RestoreStandings(standings.CurrentRank, standings.TotalCompetitors);
        }

        // Pushing on every end (not only an improvement) also syncs a best
        // restored from a local save whose earlier push never reached Firestore.
        if (_records.BestEarnings > 0.0)
        {
            _online.PushShiftEarnings(
                _records.BestEarnings, _records.BestFirstEarnedTicks, PlayerName());
        }

        // The lifetime counters ride the driver save, so bank even a shift that
        // earned nothing.
        _saver.SaveDriver(_rivals.LastResult);
        _host.ReadoutsChanged();
        _host.ShowShiftEnded(new ShiftEndedScreenViewModel(
            summary, _records, _standings, _beat,
            new RelayCommand(_host.ResumeAfterShiftEnded)));
    }

    // Calendar advance: the game-day flip (with the week it completes) and the
    // event gate the menu's calendar tile shows. The clock is read once, and a
    // tick that crosses neither boundary costs two int compares; the gate's
    // rule is only derived on the hour or day that can move it, and only its
    // flip is reported (hot-path discipline).
    private void TickCalendar(DateTime gameNow)
    {
        int today = ControlCalendar.ToDayOfWeek(gameNow);
        int hour = gameNow.Hour;
        bool dayChanged = today != _lastDay;
        if (!dayChanged && hour == _lastHour)
        {
            return;
        }

        if (dayChanged)
        {
            RollDay(today);
        }

        _lastHour = hour;
        ApplyGate(gameNow);
    }

    // Game-day flip: the calendar highlight moved.
    private void RollDay(int today)
    {
        int prev = _lastDay;
        _log.LogInfo($"calendar day changed: {prev} -> {today}");
        _lastDay = today;
        // Sunday-midnight crossing completes the week: the display week rolls
        // and the moment persists. The road-test pick deliberately does NOT
        // rotate here — an untested car stays on offer until it is actually
        // driven (PORTPLAN 49).
        if (prev == ControlCalendar.Sunday && today == ControlCalendar.Monday)
        {
            DisplayWeek++;
            // The pages repaint now instead of on the next tick: the strip and
            // the menu header carry the new week the moment it opens.
            _host.DisplayWeekChanged(DisplayWeek);
        }

        _saver.SaveClock(DisplayWeek);
    }

    /// <summary>
    /// Re-reads the event gate after something the clock did not announce
    /// changed it — an event taken from the strip, a run stamped. The flip is
    /// pushed only when the value moved, so the menu keeps its resting artwork
    /// the next time it is revealed (see <see cref="Navigation.MenuPage"/>).
    /// </summary>
    public void RefreshEventGate() => ApplyGate(_game.GameNow);

    // The gate can only move on an hour or day change (see TickCalendar) or on
    // a stamp (RefreshEventGate), so the cached pages hear the flip instead of
    // re-deriving the rule per tick.
    private void ApplyGate(DateTime gameNow)
    {
        bool playable = _gate.OpenEventNow(gameNow, DisplayWeek) is not null;
        if (playable == _eventPlayable)
        {
            return;
        }

        _eventPlayable = playable;
        _host.EventGateChanged(playable);
    }

    // Shift countdown: second-guarded so an open-but-idle shift costs one
    // string per second, never per 10 Hz tick. While a leg is live the meter
    // already repaints every tick, so this path skips it.
    private void TickShiftCountdown(DateTime now, bool legActive)
    {
        if (_shift.IsStarted && !legActive)
        {
            int secs = (int)_shift.Remaining(now).TotalSeconds;
            if (secs != _lastShiftSecs)
            {
                _lastShiftSecs = secs;
                _host.ReadoutsChanged();
            }
        }
        else if (!_shift.IsStarted)
        {
            _lastShiftSecs = -1;
        }
    }

    // The opening screen supplies the display name; before it is confirmed the
    // push stays nameless like the old placeholder.
    private string PlayerName() => _profiles.Load()?.DisplayName ?? string.Empty;
}
