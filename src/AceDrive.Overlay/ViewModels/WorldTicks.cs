using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The town's game-clock drift (PORTPLAN items 25/26/34): the citizens that
/// follow the shelves, the appetite that gains an hour at a time and the
/// nightly insurance premium. Every rule reads the shared
/// <see cref="GameClock"/> moment and guards on its own boundary, so the 10 Hz
/// poll costs one subtraction per rule and allocates nothing.
///
/// The introduction gate arrives as a value: the coordinator passes its live
/// <c>OnboardingDone</c>, the same way the day cycle takes the two job reads,
/// so this class never queries the story flow and stays a leaf.
///
/// The town's movement is reported as a return value instead of a host call:
/// only the cached delivery board page needs a repaint, and its owner (the
/// coordinator) is the one that knows the page. Nothing here reaches back into
/// the panel, so the class is testable with plain fakes.
/// </summary>
public sealed class WorldTicks
{
    private readonly GameClock _game;
    private readonly CityEconomy _city;
    private readonly Hunger _hunger;
    private readonly FleetService _fleet;
    private readonly DeliveryBoard _board;
    private readonly DeliveryCareer _career;
    // The shared driver save: an insurance charge is lost progress on a kill,
    // so it is banked through the one writer the settles already use.
    private readonly GameStateSaver _saver;
    private readonly IRivalsService _rivals;
    private readonly ILog _log;

    // The warning fires on the transition into the blocked band (Unity's TOO
    // HUNGRY edge), not per poll and not per hour.
    private bool _hungerBlocked;

    public WorldTicks(
        GameClock game, CityEconomy city, Hunger hunger, FleetService fleet,
        DeliveryBoard board, DeliveryCareer career, GameStateSaver saver,
        IRivalsService rivals, ILog log)
    {
        _game = game;
        _city = city;
        _hunger = hunger;
        _fleet = fleet;
        _board = board;
        _career = career;
        _saver = saver;
        _rivals = rivals;
        _log = log;
    }

    /// <summary>
    /// Advances every game-clock world rule to the moment the shared clock
    /// reads. Returns whether the town's cells moved: that is the transition
    /// that makes the cached delivery board stale, and most calls answer false
    /// (half citizens only bank, so the page is not rebuilt every minute).
    /// <paramref name="onboardingDone"/> is the live introduction gate: the
    /// appetite stays frozen until it closes (Unity's firstSceneComplete).
    /// </summary>
    public bool Tick(bool onboardingDone)
    {
        bool cityMoved = TickCity();
        TickHunger(onboardingDone);
        TickInsurance();
        return cityMoved;
    }

    // The town ticks on game minutes, whether or not a page is open: all work
    // happens on a whole-minute boundary. Demand needs an owned hub, like
    // Unity's gate.
    private bool TickCity()
    {
        if (!_city.AdvanceTo(_game.GameNow, _career.Owned.Count > 0))
        {
            return false;
        }

        // The log reports the town that gets banked (every cell), not the
        // career-gated header the pages show: a fresh career sees 0 on screen
        // while its stores are still drifting behind it.
        _log.LogInfo($"city: {_city.TotalCitizens(_board.Catalog.Stores)} citizens, " +
            $"{_city.TotalGrowthPerHour(_board.Catalog.Stores)}/hr");
        return true;
    }

    // Hunger meter (PORTPLAN 26, Unity GameClockManager.UpdateHunger): +1 once
    // per crossed game hour and only past onboarding, capped at the rules' max,
    // work only on a whole-hour boundary. Unity blocks the job centre at
    // maxHunger - 1 (HungerBlockPopup); the block is disabled for now — no
    // trunk or grocery store exists yet (items 51 / 26 map POI), so the gate
    // would be a one-way valve. hasPancakes stays false until a trunk lands;
    // the one-shot event then fires exactly the way Unity fires it.
    private void TickHunger(bool onboardingDone)
    {
        HungerReport report = _hunger.AdvanceTo(_game.GameNow, onboardingDone, hasPancakes: false);
        if (report.Changed)
        {
            _log.LogInfo($"[Hunger] {_hunger.Value}/{_hunger.Max}");
            // WHY bank with the hour: the meter only moves on a crossed game
            // hour, and a killed process never reaches the exit-only save —
            // every hour since the last clean exit would be lost (a 13-point
            // session went with one kill on 2026-10-25). One small write per
            // game hour, never per poll, the same policy as the premium below.
            _saver.SaveHunger();
        }

        if (_hunger.Blocks == _hungerBlocked)
        {
            return;
        }

        _hungerBlocked = _hunger.Blocks;
        if (_hungerBlocked)
        {
            _log.LogWarning("[Hunger] job gate reached (" +
                $"{_hunger.Value}/{_hunger.Max}) — the TOO HUNGRY block is disabled until a food source lands");
        }
    }

    // Nightly insurance charge (PORTPLAN 34, Unity GameClockManager.
    // UpdateInsurance): once per game date, in the charge hour. Runs on the
    // game clock, so it lands whether or not EVO is attached. A charged premium
    // is progress the player loses on a kill, so it banks now.
    private void TickInsurance()
    {
        if (_fleet.ChargeDailyInsurance())
        {
            _saver.SaveDriver(_rivals.LastResult);
        }
    }
}
