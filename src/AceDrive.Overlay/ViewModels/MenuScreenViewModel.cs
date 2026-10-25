using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The menu page: the entry a call (hotkey or wake word) opens now. The Jobs
/// tile leads into the career — the main control page with every job door —
/// the Wallet tile the account summary, the Calendar tile opens the weekly
/// strip, the Trunk tile the carried-goods page, the Home tile the player's own
/// doors (the garage and the addresses page), and the Settings tile the first-run
/// preferences form, by click or by voice. Every tile carries a voice entry; the features still to
/// land acknowledge the tap and stay put. Voice taps glow the tile teal for the
/// shared <see cref="VoiceBeat"/>; the navigating ones wait out that beat
/// before the flip, so the acknowledgment is visible, while mouse clicks act
/// at once like every other page. Marked as a jobs-side screen so the
/// onboarding gate treats it like every other post-introduction page. The
/// header repeats the design's clock (ontwerpdesign.psd): the game time over
/// the abbreviated weekday and the display week, with the wake word and the
/// hotkey reminder in the empty band above it. The hunger and filth tiles
/// show their meter as a battery: the mask reveals the art's gradient bottom
/// up, one step per point of that meter (PORTPLAN 26/33). A tap on either
/// meter tile swaps the battery for the reading as a whole percent for three
/// seconds, and the poll puts the battery back; a reading past the danger
/// band paints red.
/// </summary>
public sealed class MenuScreenViewModel : ViewModelBase, IVoiceScreen
{
    // The words are baked into the tile artwork, so the spoken keys are the
    // drawn English labels, not resx copy: a translated page would need new
    // artwork and its own keywords. Listed in the view's row order.
    // The drawn English labels double as the flash targets, so the tile names
    // live once: the array feeds the voice table and each check below.
    private const string WalletTile = "WALLET";
    private const string JobsTile = "JOBS";
    private const string CalendarTile = "CALENDAR";
    private const string TrunkTile = "TRUNK";
    private const string EmployeesTile = "EMPLOYEES";
    private const string HomeTile = "HOME";
    private const string HungerTile = "HUNGER";
    private const string FilthTile = "FILTH";
    private const string SettingsTile = "SETTINGS";

    private static readonly string[] TileLabels =
    [
        WalletTile, JobsTile, CalendarTile,
        TrunkTile, EmployeesTile, HomeTile,
        HungerTile, FilthTile, SettingsTile,
    ];

    // "job" covers the mic's singular reading; MatchLabel still answers any
    // phrase containing the whole label.
    private static readonly string[] JobsKeywords = ["JOBS", "JOB"];

    // The resx line the reading wears; the sign is copy, so it travels with
    // the translation instead of being stitched on in code.
    private const string MeterPercentKey = "MenuMeterPercent";

    // The reminder above the clock: how to call the panel back from the
    // driver's seat (it replaced the closing voice page's lesson).
    private const string WakeKey = "MenuVoiceWake";
    private const string HotkeyKey = "MenuVoiceHotkey";

    // Danger band of the revealed reading. Display data like HungerRules'
    // amber band, not economy balance: no rules field, no JSON round-trip.
    // Strictly over, so a reading that still shows 85 stays calm.
    private const int HotPercent = 85;

    /// <summary>How long a tapped meter tile shows its reading instead of the
    /// battery: long enough to read, short enough that the meter returns
    /// without a second tap.</summary>
    public static TimeSpan RevealDuration { get; } = TimeSpan.FromSeconds(3);

    private readonly VoiceBeat _beat;
    private readonly GameClock _game;
    private readonly IClock _clock;
    private readonly Hunger _hunger;
    // The fleet: the FILTH tile reads the current car's dirt (PORTPLAN 33).
    private readonly Garage _garage;
    // The brand the mic listens for, read live: the menu is cached for the
    // session, so a car switch has to repaint the wake reminder.
    private readonly Func<string> _carBrand;
    private int _week;
    private int _lastMinute = -1;
    private int _lastWeek = -1;
    private int _lastHunger = -1;
    private int _lastFilth = -1;
    // Null, not "": the first Refresh must build the wake line even when the
    // brand is still unknown.
    private string? _lastWakeBrand;

    // Voice acknowledgment state: the tile currently glowing and the beat's
    // deadline on the injected clock, never a tick count.
    private string? _flashTile;
    private DateTime _flashUntil;

    // The meter tile currently showing its reading instead of its battery,
    // and that reveal's deadline. One slot: the latest tap wins, so two
    // tiles never swap at once.
    private string? _revealTile;
    private DateTime _revealUntil;

    public MenuScreenViewModel(
        ICommand wallet, ICommand jobs, ICommand calendar, ICommand trunk, ICommand settings,
        ICommand home,
        Hunger hunger, Garage garage, Func<string> carBrand,
        VoiceBeat beat, GameClock game, IClock clock, int displayWeek)
    {
        _beat = beat;
        _game = game;
        _clock = clock;
        _hunger = hunger;
        _garage = garage;
        _carBrand = carBrand;
        _week = displayWeek;
        // The tile commands wrap the injected ones so a click is newer input
        // than a pending voice tap: it drops the deferred navigation instead
        // of letting one utterance flip the page after the player moved on.
        WalletCommand = new RelayCommand(() => Tap(wallet));
        JobsCommand = new RelayCommand(() => Tap(jobs));
        CalendarCommand = new RelayCommand(() => Tap(calendar));
        TrunkCommand = new RelayCommand(() => Tap(trunk));
        HomeCommand = new RelayCommand(() => Tap(home));
        SettingsCommand = new RelayCommand(() => Tap(settings));
        // The meter tiles have no page to open: a tap reads them out instead.
        RevealHungerCommand = new RelayCommand(() => TapReveal(HungerTile));
        RevealFilthCommand = new RelayCommand(() => TapReveal(FilthTile));
        var actions = new Dictionary<string, Action>();
        foreach (string label in TileLabels)
        {
            // Every tile answers its drawn label. The six navigating ones
            // are rebound below; the tiles whose features still have to land
            // (and the two meter tiles, rebound to their read-out) acknowledge
            // the voice tap with the teal glow and stay on the page.
            actions[label] = () => Flash(label);
        }
        foreach (string keyword in JobsKeywords)
        {
            actions[keyword] = OpenJobs;
        }
        actions[WalletTile] = OpenWallet;
        actions[CalendarTile] = OpenCalendar;
        actions[TrunkTile] = OpenTrunk;
        actions[HomeTile] = OpenHome;
        actions[SettingsTile] = OpenSettings;
        // A spoken label selects a meter tile exactly like a click, so the
        // voice tap glows and reads out in one go.
        actions[HungerTile] = () => VoiceReveal(HungerTile);
        actions[FilthTile] = () => VoiceReveal(FilthTile);
        VoiceActions = actions;
        VoiceHotkeyText = Strings.Get(HotkeyKey);
        Refresh();
    }

    /// <summary>Opens the control page (all job doors).</summary>
    public ICommand JobsCommand { get; }

    /// <summary>Opens the wallet page (cash, XP, bank, leaderboard, rating).</summary>
    public ICommand WalletCommand { get; }

    /// <summary>Opens the calendar page (the weekly strip).</summary>
    public ICommand CalendarCommand { get; }

    /// <summary>Opens the trunk page (carried goods + appetite, PORTPLAN 26).</summary>
    public ICommand TrunkCommand { get; }

    /// <summary>Opens the home page (the player's own doors, starting with the
    /// garage — Unity's YouScreen).</summary>
    public ICommand HomeCommand { get; }

    /// <summary>The menu's settings tile: the first-run preferences form
    /// until a fuller settings screen lands.</summary>
    public ICommand SettingsCommand { get; }

    /// <summary>Swaps the HUNGER tile's battery for its reading.</summary>
    public ICommand RevealHungerCommand { get; }

    /// <summary>Swaps the FILTH tile's battery for its reading.</summary>
    public ICommand RevealFilthCommand { get; }

    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool WalletFlash { get => _walletFlash; private set => SetField(ref _walletFlash, value); }
    private bool _walletFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool JobsFlash { get => _jobsFlash; private set => SetField(ref _jobsFlash, value); }
    private bool _jobsFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool CalendarFlash { get => _calendarFlash; private set => SetField(ref _calendarFlash, value); }
    private bool _calendarFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool TrunkFlash { get => _trunkFlash; private set => SetField(ref _trunkFlash, value); }
    private bool _trunkFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool EmployeesFlash { get => _employeesFlash; private set => SetField(ref _employeesFlash, value); }
    private bool _employeesFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool HomeFlash { get => _homeFlash; private set => SetField(ref _homeFlash, value); }
    private bool _homeFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool HungerFlash { get => _hungerFlash; private set => SetField(ref _hungerFlash, value); }
    private bool _hungerFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool FilthFlash { get => _filthFlash; private set => SetField(ref _filthFlash, value); }
    private bool _filthFlash;

    /// <summary>Teal-glow acknowledgment for voice taps, one per tile.</summary>
    public bool SettingsFlash { get => _settingsFlash; private set => SetField(ref _settingsFlash, value); }
    private bool _settingsFlash;

    /// <summary>Design clock line, 24-hour and culture-invariant (Tourney's
    /// digits are the design's face; the locale never reshapes "21:45").</summary>
    public string ClockText { get => _clockText; private set => SetField(ref _clockText, value); }
    private string _clockText = "";

    /// <summary>Weekday + week line under the clock ("THU / WEEK 1").</summary>
    public string DayWeekText { get => _dayWeekText; private set => SetField(ref _dayWeekText, value); }
    private string _dayWeekText = "";

    /// <summary>Wake-word line above the clock, naming the brand the mic
    /// listens for. Empty until a brand is known (the line would otherwise
    /// read "Hey "); repainted when the driven car changes.</summary>
    public string VoiceWakeText { get => _voiceWakeText; private set => SetField(ref _voiceWakeText, value); }
    private string _voiceWakeText = "";

    /// <summary>Hotkey line above the clock: the second way to call the panel.</summary>
    public string VoiceHotkeyText { get; }

    /// <summary>Hunger steps filled on the HUNGER tile: 0 (empty) .. 1 (all
    /// 36 steps lit). The tile masks its gradient to this fraction, so the
    /// colour climbs the battery one step per point.</summary>
    public double HungerFill { get => _hungerFill; private set => SetField(ref _hungerFill, value); }
    private double _hungerFill;

    /// <summary>Filth steps filled on the FILTH tile: 0 (clean) .. 1 (at the
    /// cap), masked bottom up like the hunger meter.</summary>
    public double FilthFill { get => _filthFill; private set => SetField(ref _filthFill, value); }
    private double _filthFill;

    /// <summary>Whether the HUNGER tile shows its reading in place of the
    /// battery (the tapped tile, for <see cref="RevealDuration"/>).</summary>
    public bool IsHungerRevealed
    {
        get => _isHungerRevealed;
        private set => SetField(ref _isHungerRevealed, value);
    }
    private bool _isHungerRevealed;

    /// <summary>Whether the FILTH tile shows its reading in place of the
    /// battery.</summary>
    public bool IsFilthRevealed
    {
        get => _isFilthRevealed;
        private set => SetField(ref _isFilthRevealed, value);
    }
    private bool _isFilthRevealed;

    /// <summary>The hunger reading as the localized whole percent the tapped
    /// tile draws.</summary>
    public string HungerPercentText
    {
        get => _hungerPercentText;
        private set => SetField(ref _hungerPercentText, value);
    }
    private string _hungerPercentText = "";

    /// <summary>The filth reading as the localized whole percent.</summary>
    public string FilthPercentText
    {
        get => _filthPercentText;
        private set => SetField(ref _filthPercentText, value);
    }
    private string _filthPercentText = "";

    /// <summary>True once the hunger reading is past the danger band: the
    /// view paints the number red.</summary>
    public bool HungerPercentHot
    {
        get => _hungerPercentHot;
        private set => SetField(ref _hungerPercentHot, value);
    }
    private bool _hungerPercentHot;

    /// <summary>True once the filth reading is past the danger band.</summary>
    public bool FilthPercentHot
    {
        get => _filthPercentHot;
        private set => SetField(ref _filthPercentHot, value);
    }
    private bool _filthPercentHot;

    /// <summary>Display week from the screen controller: the menu is built
    /// once per session, so the Sunday rollover pushes the new number in.</summary>
    public int DisplayWeek
    {
        get => _week;
        set
        {
            if (SetField(ref _week, value)) Refresh();
        }
    }

    /// <summary>
    /// Repaints the header, the two battery fills and the readings a reveal
    /// would draw. Called from the 10 Hz poll; the minute/week/level/brand
    /// guards keep it allocation-free between changes (hot-path discipline). A
    /// hunger point, a dirtier car or a switch to another brand repaints even
    /// inside the same game minute, so a wash or the new wake word shows at
    /// once — also on a number still on screen.
    /// </summary>
    public void Refresh()
    {
        DateTime now = _game.GameNow;
        int minute = now.Hour * 60 + now.Minute;
        int hunger = _hunger.Value;
        int filth = _garage.Current?.Filth.Value ?? 0;
        string brand = _carBrand();
        if (minute == _lastMinute && _week == _lastWeek
            && hunger == _lastHunger && filth == _lastFilth
            && string.Equals(brand, _lastWakeBrand, StringComparison.Ordinal)) return;
        _lastMinute = minute;
        _lastWeek = _week;
        _lastHunger = hunger;
        _lastFilth = filth;
        _lastWakeBrand = brand;
        ClockText = GameClockText.Time(now);
        DayWeekText = GameClockText.DayWeek(_game.Today, _week);
        VoiceWakeText = WakeText(brand);
        // The fills are fractions of each meter's own cap: hunger walks its 36
        // steps, filth the car-wash rules' 25. Never a fixed denominator, or a
        // filthy car would stop short of a full battery.
        HungerFill = (double)hunger / _hunger.Max;
        FilthFill = (double)filth / _garage.FilthRules.MaxFilth;
        // The reading and its colour come off one whole percent, so the number
        // the player sees and the band the colour tests can never disagree.
        int hungerPercent = Percent(HungerFill);
        int filthPercent = Percent(FilthFill);
        HungerPercentText = Strings.Format(MeterPercentKey, hungerPercent);
        FilthPercentText = Strings.Format(MeterPercentKey, filthPercent);
        HungerPercentHot = hungerPercent > HotPercent;
        FilthPercentHot = filthPercent > HotPercent;
    }

    // Whole percent of the fill, halves away from zero: the battery's own
    // fraction, never the raw step count, so both tiles read on one scale.
    private static int Percent(double fill) =>
        (int)Math.Round(fill * 100.0, MidpointRounding.AwayFromZero);

    // The wake reminder names the car the mic listens for, in caps like the
    // voice page drew it. No brand yet means no line at all: the copy would
    // otherwise name an empty car ("... For instance "Hey ".").
    private static string WakeText(string brand) =>
        string.IsNullOrWhiteSpace(brand)
            ? ""
            : Strings.Format(WakeKey, brand.ToUpperInvariant());

    // Voice taps glow first and wait out the shared beat, so the
    // acknowledgment is visible before the page flips.
    private void OpenWallet() => FireVoice(WalletTile, WalletCommand);

    private void OpenJobs() => FireVoice(JobsTile, JobsCommand);

    private void OpenCalendar() => FireVoice(CalendarTile, CalendarCommand);

    private void OpenTrunk() => FireVoice(TrunkTile, TrunkCommand);

    private void OpenHome() => FireVoice(HomeTile, HomeCommand);

    private void OpenSettings() => FireVoice(SettingsTile, SettingsCommand);

    // A click bypasses the beat (companion parity): it acts at once, and it
    // supersedes a voice tap still waiting, so one utterance cannot flip the
    // page after the player already moved on. Leaving a page also drops any
    // reading still on show, so a cached menu never reopens mid-reveal.
    private void Tap(ICommand command)
    {
        _beat.Cancel();
        ClearFlash();
        ClearReveal();
        command.Execute(null);
    }

    // The meter tiles act by reading out, so they follow the click rules
    // without navigating.
    private void TapReveal(string tile)
    {
        _beat.Cancel();
        ClearFlash();
        Reveal(tile);
    }

    private void VoiceReveal(string tile)
    {
        Flash(tile);
        Reveal(tile);
    }

    private void Reveal(string tile)
    {
        _revealTile = tile;
        _revealUntil = _clock.UtcNow + RevealDuration;
        IsHungerRevealed = tile == HungerTile;
        IsFilthRevealed = tile == FilthTile;
    }

    /// <summary>
    /// Puts the battery back once the reveal's beat is up. Called from the
    /// 10 Hz poll beside <see cref="UpdateFlash"/>: one DateTime compare per
    /// tick, allocations only on the swap back.
    /// </summary>
    public void UpdateReveal()
    {
        if (_revealTile is not null && _clock.UtcNow >= _revealUntil)
        {
            ClearReveal();
        }
    }

    private void ClearReveal()
    {
        _revealTile = null;
        IsHungerRevealed = false;
        IsFilthRevealed = false;
    }

    private void FireVoice(string tile, ICommand command)
    {
        Flash(tile);
        _beat.Defer(() =>
        {
            // Clear on the way out so the cached menu never shows a stale
            // glow when the player comes back (bank-page precedent).
            ClearFlash();
            command.Execute(null);
        });
    }

    private void Flash(string tile)
    {
        WalletFlash = tile == WalletTile;
        JobsFlash = tile == JobsTile;
        CalendarFlash = tile == CalendarTile;
        TrunkFlash = tile == TrunkTile;
        EmployeesFlash = tile == EmployeesTile;
        HomeFlash = tile == HomeTile;
        HungerFlash = tile == HungerTile;
        FilthFlash = tile == FilthTile;
        SettingsFlash = tile == SettingsTile;
        _flashTile = tile;
        _flashUntil = _beat.Deadline();
    }

    /// <summary>
    /// Clears the expired voice-flash. Called from the 10 Hz poll (the refresh
    /// guard would swallow a same-minute expiry): one DateTime compare per
    /// tick, allocations only on the glow transition.
    /// </summary>
    public void UpdateFlash()
    {
        if (_flashTile is not null && _beat.IsDue(_flashUntil))
        {
            ClearFlash();
        }
    }

    // Glow off on every tile at once: the deferred navigation clears here on
    // the way out, the poll clears here when the beat expires.
    private void ClearFlash()
    {
        _flashTile = null;
        WalletFlash = false;
        JobsFlash = false;
        CalendarFlash = false;
        TrunkFlash = false;
        EmployeesFlash = false;
        HomeFlash = false;
        HungerFlash = false;
        FilthFlash = false;
        SettingsFlash = false;
    }
}
