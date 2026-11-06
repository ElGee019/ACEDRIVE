using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Friday's pancake visit (PORTPLAN items 26/52, Unity's Grandma pancake event):
/// the calendar row opens a briefing, START pins grandma's house on the
/// companion, and ENTER at her door serves the plate — the appetite drops by the
/// catalogue's hunger value and she packs one more stack for the road, which is
/// the only place the appetite's one-shot event can find pancakes. The week's
/// stamp is written when the meal is served, not when the page opens, so a
/// process that dies on the drive leaves the row playable.
///
/// The visit is session state (like the missing-person run): nothing about the
/// drive is persisted, and a restart inside the same game day simply reopens the
/// briefing. The meal banks the driver and the appetite together, because a
/// killed process would otherwise lose both.
/// </summary>
public sealed class PancakeFlow
{
    private readonly GameClock _game;
    // Grandma's door comes from the player's own profile: the house the intro's
    // pancake leg already sent them to. Null when the save names no street, so
    // the row refuses instead of pinning a door that does not exist.
    private readonly Func<AddressLocation?> _home;
    private readonly Trunk _trunk;
    private readonly Hunger _hunger;
    private readonly StoreCatalog _catalog;
    private readonly ITelemetryHud? _hud;
    private readonly GameStateSaver _saver;
    private readonly IRivalsService _rivals;
    private readonly WeeklyEventLedger _weekly;
    private readonly VoiceBeat _beat;
    private readonly ILog _log;

    // TEMPORARY (testing): true skips the Friday window and the weekly stamp, so
    // the visit can be driven again at any time. Only the tests set it; the app
    // keeps the shipping gates.
    private readonly bool _ignoreEventGates;

    // WHY late: the host is the coordinator, which is built after this flow. The
    // attach closes in the coordinator's constructor, before a calendar tap can
    // reach it.
    private IPancakeHost _host = null!;

    private PancakePhase _phase = PancakePhase.Idle;
    private PointOfInterest? _door;

    public PancakeFlow(
        GameClock game, Func<AddressLocation?> home, Trunk trunk, Hunger hunger,
        StoreCatalog catalog, ITelemetryHud? hud, GameStateSaver saver,
        IRivalsService rivals, WeeklyEventLedger weekly, VoiceBeat beat, ILog log,
        bool ignoreEventGates = false)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(home);
        ArgumentNullException.ThrowIfNull(trunk);
        ArgumentNullException.ThrowIfNull(hunger);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(saver);
        ArgumentNullException.ThrowIfNull(rivals);
        ArgumentNullException.ThrowIfNull(weekly);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(log);

        _game = game;
        _home = home;
        _trunk = trunk;
        _hunger = hunger;
        _catalog = catalog;
        _hud = hud;
        _saver = saver;
        _rivals = rivals;
        _weekly = weekly;
        _beat = beat;
        _log = log;
        _ignoreEventGates = ignoreEventGates;
    }

    /// <summary>Where the visit stands; session state, never persisted.</summary>
    public PancakePhase Phase => _phase;

    /// <summary>
    /// True while a leg owns the companion (grandma's door is pinned). The
    /// calendar refuses the other rows then, so no second event can repin the
    /// HUD underneath the drive; a briefing page on its own does not hold the
    /// companion and so does not lock the strip.
    /// </summary>
    public bool InFlight => _phase is PancakePhase.Driving;

    /// <summary>Attaches the coordinator that owns the panel and the page policy.</summary>
    public void Attach(IPancakeHost host) => _host = host;

    /// <summary>
    /// The calendar's Friday tap. The gate is re-checked here as well as by the
    /// strip, so a caller can never jump it; a visit already in flight reopens
    /// its briefing instead of drawing a second leg.
    /// </summary>
    public void Begin()
    {
        DateTime now = _game.GameNow;
        if (!_ignoreEventGates
            && ControlCalendar.EventOfDay(ControlCalendar.ToDayOfWeek(now)) != ControlCalendar.Pancakes)
        {
            _log.LogInfo("pancake visit ignored: today is not the pancake row's weekday");
            return;
        }
        if (!_ignoreEventGates && !ControlCalendar.IsEventWindow(now.Hour))
        {
            _log.LogInfo("pancake visit ignored: outside the 8am-8pm window");
            return;
        }
        if (!_ignoreEventGates && _weekly.TakenOn(ControlCalendar.Pancakes, now))
        {
            _log.LogInfo("pancake visit ignored: this week is already stamped");
            return;
        }
        if (_phase is PancakePhase.Driving)
        {
            // A live leg is re-pinned instead of drawn again: the row is how a
            // stalled guide comes back, and the door is the one already chosen.
            Drive();
            return;
        }
        if (_phase is PancakePhase.Served)
        {
            _log.LogInfo("pancake visit ignored: the receipt is up");
            return;
        }

        _door = HomeDoor();
        if (_door is not { } door)
        {
            _log.LogWarning("pancake visit not opened: the profile names no home address");
            return;
        }

        _phase = PancakePhase.Briefing;
        _host.Present(BriefingPage(door));
        _log.LogInfo(
            $"pancake friday opened: {AddressFormatter.Format(door.Address)}, " +
            $"hunger {_hunger.Value}/{_hunger.Max}");
    }

    /// <summary>
    /// The briefing's START: pins grandma's house and hides the panel so the
    /// companion guides the drive. A live leg re-pins from the row's recovery
    /// tap; a served or idle visit refuses silently, so a stale page can never
    /// start a second leg.
    /// </summary>
    public void Drive()
    {
        if (_phase is PancakePhase.Served or PancakePhase.Idle || _door is not { } door)
        {
            return;
        }

        _phase = PancakePhase.Driving;
        _hud?.SetDestination(
            door, new HudDestinationCopy("HudPancakeDrive", "HudPancakeArrived", "HudPancakes"));
        _host.HidePanel();
        _log.LogInfo($"pancake visit: driving to {AddressFormatter.Format(door.Address)}");
    }

    /// <summary>
    /// The briefing's BACK: declines the visit and hands the player back to the
    /// calendar they tapped the row on. Nothing was stamped, so the row stays
    /// playable.
    /// </summary>
    public void Back()
    {
        if (_phase is not PancakePhase.Briefing)
        {
            return;
        }

        _phase = PancakePhase.Idle;
        _door = null;
        _host.ShowCalendar();
        _log.LogInfo("pancake visit declined: back to the calendar");
    }

    /// <summary>
    /// The receipt's CONTINUE: closes the visit onto the main page.
    /// </summary>
    public void Continue()
    {
        if (_phase is not PancakePhase.Served)
        {
            return;
        }

        _phase = PancakePhase.Idle;
        _door = null;
        _host.ShowMenu();
    }

    /// <summary>
    /// ENTER while the drive is live. True when this tap was the visit's — the
    /// meal is served and the receipt opens; false lets the door answer like any
    /// other story door, so a tap elsewhere in town is never swallowed.
    /// </summary>
    public bool TryEnter()
    {
        if (_phase is not PancakePhase.Driving || _door is not { } door)
        {
            return false;
        }
        if (_hud is null || !_hud.ParkedAtTarget || _hud.CurrentDestination is not { } at)
        {
            return false;
        }
        if (!at.Address.Matches(door.Address))
        {
            return false;
        }

        Serve();
        return true;
    }

    // The meal, the stamp and the receipt, in the order Unity banks them: the
    // appetite and the trunk move first, the stamp is written before the driver
    // file so a kill between the two can never re-open a served week, then both
    // saves run together. The HUD releases the door so a later ENTER cannot
    // serve a second plate.
    private void Serve()
    {
        if (PancakeVisit.Serve(_trunk, _hunger, _catalog) is not { } meal)
        {
            _log.LogWarning("pancake visit not served: the item catalogue lost the pancake row");
            _hud?.SetDestination(null);
            _phase = PancakePhase.Idle;
            _door = null;
            return;
        }

        _weekly.MarkTaken(ControlCalendar.Pancakes, _game.GameNow);
        _saver.SaveDriver(_rivals.LastResult);
        _saver.SaveHunger();
        _hud?.SetDestination(null);
        _phase = PancakePhase.Served;
        _log.LogInfo(
            $"pancake visit served: hunger {meal.Before} -> {meal.After} " +
            $"(-{meal.Served}), {PancakeVisit.ItemName} packed for the road");
        _host.Present(ReceiptPage(meal));
    }

    // The two pages take data and commands, never the flow: a page that knew the
    // flow could start a second drive behind the coordinator's back.
    private PancakeBriefingScreenViewModel BriefingPage(PointOfInterest door) =>
        new(
            Strings.Format("PancakeBriefingVenue", AddressFormatter.Format(door.Address)),
            Strings.Format("TrunkHunger", _hunger.Value, _hunger.Max),
            _hunger.IsAmber,
            Drive,
            new RelayCommand(Back),
            _beat);

    private PancakeReceiptScreenViewModel ReceiptPage(PancakeMeal meal) =>
        new(meal, _hunger.Max, new RelayCommand(Continue), _beat);

    // Grandma's house on the player's own street: the same door the intro's
    // pancake leg pinned, rebuilt from the profile so the weekly visit never
    // sits on a street the save no longer names.
    private PointOfInterest? HomeDoor() =>
        _home() is { } address
            ? new PointOfInterest(
                StoryHome.Definition.Id, StoryHome.Definition.Name, address,
                StoryHome.Definition.Type, StoryHome.Definition.OwnerName,
                StoryHome.Definition.Category, StoryHome.Definition.GuaranteedItems)
            : null;
}
