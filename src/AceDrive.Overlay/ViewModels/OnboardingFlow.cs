using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The introduction walk (PORTPLAN Career Start items 1-5 + item 54):
/// preferences, opening screen, brand, grandma's car, voice info, birthday
/// dialogue, and the errand it hands over. It owns the persisted progress and
/// profile, so the pages talk to this flow through <see cref="IScreenController"/>
/// instead of the whole shell.
/// </summary>
public sealed class OnboardingFlow : IScreenController
{
    private readonly IOnboardingStore _onboarding;
    private readonly IContentSource _content;
    private readonly IProfileStore _profiles;
    private readonly IPlayerNameRegistry _playerNames;
    private readonly AddressMap _addressMap;
    private readonly string? _detectedPlayerName;
    private readonly PoiGenerator _pois;
    // Every discovered door lands here (PORTPLAN 24/27); the addresses page
    // reads it and the HUD's ENTER routes from the same destination.
    private readonly PoiDirectory _poiDirectory;
    private readonly IPoiDirectoryStore _poiStore;
    private readonly PoiArrivalRouter? _poiArrivals;
    private readonly Trunk _trunk;
    private readonly IPreferencesStore _preferencesStore;
    private readonly ICultureService _culture;
    private readonly VoiceBeat _beat;
    private readonly ITelemetryHud? _hud;
    private readonly Garage _garage;
    private readonly Func<int> _displayWeek;
    private readonly Action _saveGarage;
    // The brand the wake word listens for (the car currently driven); read at
    // page-build time so the hotkey lesson always names the player's own car.
    private readonly Func<string> _carBrand;
    private readonly ILog _log;
    private readonly IStoryHost _host;
    // Story destinations that are not shop interiors: the friend pickups,
    // grandma's pancakes and the community centre. Registering the action here
    // keeps a new leg out of EnterPoi's branching.
    private readonly Dictionary<string, Action<PointOfInterest>> _storyArrivals;

    // WHY read through on demand: Data/*.json edits (wake aliases, transcript
    // confusions) take effect while the overlay runs; pages built later pick
    // up the new snapshot automatically.
    private OnboardingContent Content => _content.Content;

    private OnboardingState _progress;
    private PlayerProfile? _profile;
    private PlayerPreferences _preferences;
    // The live leg's door: the birthday errand first, then the friends'
    // houses (PORTPLAN 5/24/27). Pinned on the HUD and used by ENTER.
    private PointOfInterest? _poi;

    // The errand's guaranteed items and their stack sizes when its door
    // opened. The trunk survives sessions, so BACK must only bank a visit
    // that actually bought what this tour asked for.
    private Dictionary<string, int>? _errandStockAtEntry;

    // Unity's task lines for the two friend legs (MapScreen taskTexts[11] and
    // [5]); the HUD joins the hint to the address it renders.
    private const string JustinHint = "HudHintJustin";
    private const string AnnabellaHint = "HudHintAnnabella";
    // Settings reuses the first-run form but exits to the menu, not the walk.
    private const string SettingsDoneLabelKey = "PreferencesBack";
    // The Vosk model is English, so the spoken key stays BACK even when the
    // button copy is translated to TERUG.
    private const string SettingsVoiceBackLabel = "BACK";

    // Unity's TaskTexts[6] plus the PANCAKES! arrival from MapScreen: the home
    // leg owns all three HUD lines.
    private static readonly HudDestinationCopy HomeCopy = new(
        "HudHintHome", "HudArrivedHome", "HudPancakes");

    // Unity's taskTexts[7] and [8]: after pancakes the player drives to the
    // community centre, then enters for the taxi-license conversation.
    private static readonly HudDestinationCopy CommunityCentreCopy = new(
        "HudHintCommunityCentre", "HudArrivedCommunityCentre", "HudEnter");

    public OnboardingFlow(
        IOnboardingStore onboarding, IContentSource content,
        IProfileStore profiles, IPlayerNameRegistry playerNames,
        AddressMap addressMap, string? detectedPlayerName,
        PoiGenerator pois, PoiDirectory poiDirectory, IPoiDirectoryStore poiStore,
        PoiArrivalRouter? poiArrivals, Trunk trunk,
        IPreferencesStore preferencesStore, PlayerPreferences preferences,
        ICultureService culture, VoiceBeat beat, ITelemetryHud? hud,
        Garage garage, Func<int> displayWeek, Action saveGarage,
        Func<string> carBrand, ILog log, IStoryHost host)
    {
        _onboarding = onboarding;
        _content = content;
        _profiles = profiles;
        _playerNames = playerNames;
        _addressMap = addressMap;
        _detectedPlayerName = detectedPlayerName;
        _pois = pois;
        _poiDirectory = poiDirectory;
        _poiStore = poiStore;
        _poiArrivals = poiArrivals;
        _trunk = trunk;
        _preferencesStore = preferencesStore;
        _preferences = preferences;
        _culture = culture;
        _beat = beat;
        _hud = hud;
        _garage = garage;
        _displayWeek = displayWeek;
        _saveGarage = saveGarage;
        _carBrand = carBrand;
        _log = log;
        _host = host;
        _storyArrivals = new Dictionary<string, Action<PointOfInterest>>(StringComparer.Ordinal)
        {
            [PoiTypes.Person] = PickUpFriend,
            [PoiTypes.Home] = _ => EnterHome(),
            [PoiTypes.CommunityCentre] = _ => EnterCommunityCentre(),
        };
        _progress = _onboarding.Load();
        _profile = _profiles.Load();
        RestoreSavedPois();
        if (_hud is not null)
        {
            _hud.EnterRequested += (_, _) => EnterPoi();
        }
    }

    /// <summary>The persisted walk state (saved before the flow loaded it).</summary>
    public OnboardingState Progress => _progress;

    /// <summary>The confirmed profile, or null before the opening screen.</summary>
    public PlayerProfile? Profile => _profile;

    /// <summary>True once the birthday dialogue is done on disk.</summary>
    public bool Done => _progress.IsComplete;

    /// <summary>
    /// The walk page first paint opens on: the one the player closed the
    /// overlay on, or the preferences page for a fresh install. Past the
    /// birthday the caller opens the menu instead, so this only ever returns
    /// an introduction page. The chain ends on the menu, so the walk is never
    /// a dead end. No new IScreenController verbs: each page
    /// takes its actions by constructor (composition over interface growth).
    /// </summary>
    public object? StartPage() => ResumeStage() switch
    {
        OnboardingStage.Opening => OpeningPage(),
        OnboardingStage.Brand => BrandPage(),
        OnboardingStage.GrandmaCar => CarPage(),
        OnboardingStage.VoiceInfo => VoiceIntroPage(),
        OnboardingStage.Dialogue => BirthdayPage(),
        _ => PreferencesPage(),
    };

    /// <summary>
    /// Pins the tour leg a finished introduction hands over to, so a relaunch
    /// that opens on the menu still tells the player where to drive. Every
    /// leg is banked, so this only re-reads the current one.
    /// </summary>
    public void ResumeTour()
    {
        if (!Done) return;
        // The birthday hands the errand over before the friend tour; resume
        // the same first leg so a relaunch cannot skip Max's store.
        if (!_progress.MaxErrandDone)
        {
            _poi = BirthdayErrand();
            HandOverErrand();
            return;
        }

        ShowNextLeg();
    }

    // Saves written before the stage was banked only carry their picks; the
    // furthest page those picks imply is the closest honest resume point. A
    // fresh install has none of them and still opens on preferences.
    private OnboardingStage ResumeStage()
    {
        if (_progress.Stage != OnboardingStage.Preferences)
        {
            return _progress.Stage;
        }

        if (_progress.MaxStoreAddress is not null) return OnboardingStage.Dialogue;
        if (_progress.GrandmaCarId is not null) return OnboardingStage.VoiceInfo;
        if (_progress.FavoriteBrand is not null) return OnboardingStage.GrandmaCar;
        return _profile is not null ? OnboardingStage.Brand : OnboardingStage.Preferences;
    }

    // The walk's page is part of the save, so exiting on any page resumes
    // there next launch instead of replaying from the first one.
    private void SaveStage(OnboardingStage stage)
    {
        if (_progress.Stage == stage) return;
        _progress = _progress with { Stage = stage };
        _onboarding.Save(_progress);
    }

    private PreferencesScreenViewModel PreferencesPage() =>
        new(_preferences, OnPreferencesChanged, ShowOpening);

    /// <summary>
    /// Builds the preferences form for the settings entry: the same unit +
    /// language picker the first-run walk opens on, labelled BACK instead of
    /// CONTINUE and answering that word from the mic. Its action hands back to
    /// the caller's page instead of advancing the walk, while a selection still
    /// saves and applies through the walk's one preferences writer. The
    /// optional reset action adds the settings page's destructive button; the
    /// first-run page passes none, so it never offers to wipe a career.
    /// </summary>
    public PreferencesScreenViewModel CreateSettingsPage(Action done, Func<bool>? resetGame = null) =>
        new(_preferences, OnPreferencesChanged, done,
            SettingsDoneLabelKey, SettingsVoiceBackLabel, resetGame);

    private object? OpeningPage()
    {
        // Missing content degrades like the first visit: past the birthday the
        // control page still opens, before it there is no page to show.
        if (Content.Brands.Count == 0)
        {
            return _progress.IsComplete ? _host.ControlScreen() : null;
        }

        return new OpeningScreenViewModel(
            _addressMap, _profile, _detectedPlayerName, _playerNames, CompleteOpening);
    }

    private object? BrandPage()
    {
        if (Content.Brands.Count == 0)
        {
            return _progress.IsComplete ? _host.ControlScreen() : null;
        }

        return new BrandScreenViewModel(this, Content.Brands);
    }

    private GrandmaCarScreenViewModel CarPage() => new GrandmaCarScreenViewModel(this, Content.Cars);

    private VoiceInfoScreenViewModel VoiceIntroPage() => new VoiceInfoScreenViewModel(
        Strings.Get("VoiceInfoIntro"),
        Strings.Format("VoiceInfoButtons",
            Strings.Get("DialogueContinue").ToUpperInvariant()),
        _beat, ShowDialogue);

    // Rebuilds the birthday conversation on its own: resuming a page must not
    // replay the voice-info acknowledgment log of the transition into it.
    private DialogueScreenViewModel BirthdayPage()
    {
        _poi = BirthdayErrand();
        return new DialogueScreenViewModel(
            FillDialogue(Content.Dialogue, _poi), Strings.Get("BirthdayTitle"),
            _beat, FinishDialogue);
    }

    public void ChooseBrand(string brand)
    {
        if (!Content.Brands.Contains(brand)) return;
        _progress = _progress with
        {
            FavoriteBrand = brand,
            Stage = OnboardingStage.GrandmaCar,
        };
        _onboarding.Save(_progress);
        _log.LogInfo($"favorite brand: {brand}");
        _host.ShowScreen(CarPage());
    }

    public void ChooseCar(string carId)
    {
        if (!Content.Cars.Any(c => c.Id == carId)) return;
        _progress = _progress with
        {
            GrandmaCarId = carId,
            Stage = OnboardingStage.VoiceInfo,
        };
        _onboarding.Save(_progress);
        // Unity GrandmaCarScreen: the pick IS the first OwnedCar (odometer 0,
        // no insurance) and the car driven from here on. Walking the
        // introduction again keeps an owned car, but the pick still becomes
        // current: the player just told us which car they drive.
        bool added = false;
        if (!_garage.Owns(carId))
        {
            _garage.Add(carId, _displayWeek());
            added = true;
            _log.LogInfo($"grandma car: {carId} (first OwnedCar)");
        }
        else
        {
            _log.LogInfo($"grandma car: {carId}");
        }
        bool selected = _garage.SetCurrent(carId);
        if (added || selected)
        {
            _saveGarage();
        }
        _host.ShowScreen(VoiceIntroPage());
    }

    public void ShowDialogue()
    {
        _log.LogInfo("voice info acknowledged: showing birthday dialogue");
        SaveStage(OnboardingStage.Dialogue);
        _host.ShowScreen(BirthdayPage());
    }

    // Unity banks maxsStoreAddress: the shop is drawn once, then every launch
    // and every later story beat reuses the same door. A save from before the
    // address was banked draws one now, like the original introduction.
    private PointOfInterest? BirthdayErrand()
    {
        if (_progress.MaxStoreAddress is { } stored
            && _progress.MaxStoreId is { } id
            && _pois.Place(id, stored) is { } banked)
        {
            Remember(banked);
            return banked;
        }

        PointOfInterest? drawn = _pois.Spawn(PoiCatalog.MaxGroceryId);
        if (drawn is not null)
        {
            _progress = _progress with
            {
                MaxStoreId = drawn.Id,
                MaxStoreAddress = drawn.Address,
            };
            _onboarding.Save(_progress);
            Remember(drawn);
        }

        return drawn;
    }

    public void FinishDialogue()
    {
        _progress = _progress with { BirthdayDone = true };
        _onboarding.Save(_progress);
        // The HUD is the reminder of where to drive; pin the errand as the
        // dialogue hands it over (PORTPLAN 5/24/27). A save that already
        // carries the story item skips straight to the next phase.
        HandOverErrand();
        _log.LogInfo("birthday dialogue done: main page unlocked");
        // The intro hands over on the menu and leaves the panel up, so the
        // player sees the page they will call back from now on; idle-hide and
        // the hotkey own it from here. The HUD is revealed behind the panel
        // and steps forward once the panel closes.
        _host.ShowScreen(_host.MenuScreen());
        _hud?.Reveal();
    }

    // Unity's PANCAKES! arrival at grandma's house: the first tour stop that
    // can hand over a dialogue from the HUD. The addresses page keeps the
    // house listed after the tour, so a later visit is a plain closed door:
    // the pancake conversation only opens on its live leg.
    private void EnterHome()
    {
        if (!_progress.AnnabellaPickedUp || _progress.PancakesDone)
        {
            return;
        }

        ShowPancakeDialogue();
    }

    private void ShowPancakeDialogue() =>
        ShowStoryDialogue(
            Content.PancakeDialogue, "PancakeTitle", "pancake", FinishPancakes);

    private void FinishPancakes()
    {
        _progress = _progress with { PancakesDone = true };
        _onboarding.Save(_progress);
        _log.LogInfo("pancake dialogue done: heading to the community centre");
        // The table clears and the next task appears on the HUD before the
        // panel leaves: the drive to the community centre is the only visible
        // instruction until the player calls the overlay back.
        ShowNextLeg();
        _host.ShowScreen(_host.ControlScreen());
        _host.Hide();
    }

    // The community-centre arrival grants the taxi license. Its Continue banks
    // the visit so the HUD never sends a licensed driver back to the same door.
    // The address stays on the addresses page, but a later visit is closed:
    // the license conversation only opens on its live leg.
    private void EnterCommunityCentre()
    {
        if (!_progress.PancakesDone || _progress.CommunityCentreDone)
        {
            return;
        }

        ShowCommunityCentreDialogue();
    }

    private void ShowCommunityCentreDialogue() =>
        ShowStoryDialogue(
            Content.CommunityCentreDialogue, "CommunityCentreTitle",
            "community centre", FinishCommunityCentre);

    // Both post-birthday conversations pop the main panel like a POI interior
    // and use the same one-page-at-a-time hand-over. A second ENTER while the
    // conversation is open must not restart it at page one.
    private void ShowStoryDialogue(
        IReadOnlyList<DialogueLine> lines, string titleKey, string label,
        Action onFinished)
    {
        if (_host.IsShown && _host.CurrentScreen is DialogueScreenViewModel)
        {
            return;
        }

        if (lines.Count == 0)
        {
            _log.LogWarning($"{label} dialogue missing; skipping the arrival");
            onFinished();
            return;
        }

        _log.LogInfo($"{label} dialogue shown");
        _host.Present(new DialogueScreenViewModel(
            FillPlayerDialogue(lines), Strings.Get(titleKey), _beat, onFinished));
    }

    private void FinishCommunityCentre()
    {
        _progress = _progress with { CommunityCentreDone = true };
        _onboarding.Save(_progress);
        _poi = null;
        _hud?.SetDestination(null);
        // First exit from the centre teaches how to call the panel back from
        // the driver's seat: the wake word, plus the hotkey for a muted mic.
        // CONTINUE closes the panel exactly as this leg did before.
        _host.ShowScreen(new VoiceInfoScreenViewModel(
            Strings.Format("VoiceInfoWake", _carBrand().ToUpperInvariant()),
            Strings.Get("VoiceInfoHotkey"),
            _beat, CloseCommunityCentrePage,
            Strings.Format("VoiceInfoFirstJob",
                Strings.Get("DialogueContinue").ToUpperInvariant())));
        _log.LogInfo("community centre dialogue done: taxi license signed");
    }

    private void CloseCommunityCentrePage()
    {
        _host.ShowScreen(_host.ControlScreen());
        _host.Hide();
    }

    // The errand line names the business and a random house; ordinary pages
    // pass through untouched. A null POI (no content or no usable address)
    // leaves the authored text as-is instead of trapping the walk.
    private static IReadOnlyList<DialogueLine> FillDialogue(
        IReadOnlyList<DialogueLine> lines, PointOfInterest? poi) =>
        poi is { } destination
            ? DialogueText.Fill(
                lines, destination.Name, AddressFormatter.Format(destination.Address))
            : lines;

    // The pancake and community-centre tables address the player by name
    // ([YOU]); the errand tokens do not appear there.
    private IReadOnlyList<DialogueLine> FillPlayerDialogue(
        IReadOnlyList<DialogueLine> lines) =>
        DialogueText.Fill(
            lines, string.Empty, string.Empty, _profile?.DisplayName ?? string.Empty);

    // Pins the errand until this tour's store visit is banked. The trunk can
    // already hold maple syrup from an earlier run (the inventory persists),
    // so only the onboarding leg flag — not the item — decides whether the
    // player has visited Max in this tour.
    private void HandOverErrand()
    {
        if (_poi is not { } poi)
        {
            _hud?.SetDestination(null);
            return;
        }

        if (_progress.MaxErrandDone)
        {
            _log.LogInfo($"birthday errand already complete: {poi.Name}");
            AdvancePastErrand(poi);
            return;
        }

        _hud?.SetDestination(poi);
        Remember(poi);
        _log.LogInfo($"birthday errand: {poi.Name} at {AddressFormatter.Format(poi.Address)}");
    }

    // The HUD's ENTER button fires only once the player is parked on the
    // destination's house. Friends and grandma's house have no interior: the
    // story arrivals own those taps (pickup, pancakes). Otherwise the typed
    // router builds the shop interior (PORTPLAN 24); an unclaimed type keeps
    // the log-only fallback so no POI is a dead end. A second ENTER while
    // already inside is ignored. The interior's BACK is bound to this POI, so
    // leaving it can only ever finish this errand.
    private void EnterPoi()
    {
        // The HUD knows which door it is guiding to: a story leg pins one, and
        // the addresses page pins another through the same SetDestination, so
        // one ENTER enters whichever place the player actually drove to.
        if ((_hud?.CurrentDestination ?? _poi) is not { } poi)
        {
            return;
        }

        if (_storyArrivals.TryGetValue(poi.Type, out Action<PointOfInterest>? arrive))
        {
            arrive(poi);
            return;
        }

        // Already inside: a second ENTER only brings a hidden panel back,
        // never rebuilds the page (and never loses the basket).
        if (_host.CurrentScreen is IPoiScreen current)
        {
            if (!_host.IsShown)
            {
                _host.Present(current);
            }
            return;
        }

        object? screen = _poiArrivals?.Open(
            poi, new RelayCommand(() => LeavePoi(poi)), OnPoiChanged);
        _errandStockAtEntry = StockOf(poi);
        _log.LogInfo($"entered {poi.Name} at {AddressFormatter.Format(poi.Address)}");
        if (screen is not null)
        {
            _host.Present(screen);
        }
    }

    // The pickup is banked: the walk replays on every launch and a returning
    // player must not be sent back to a friend already in the car. Justin hands
    // over to Annabella; picking her up hands the tour to the drive home.
    private void PickUpFriend(PointOfInterest friend)
    {
        if (friend.Id == StoryFriends.Justin.Id)
        {
            if (_progress.JustinPickedUp)
            {
                return;
            }

            _progress = _progress with { JustinPickedUp = true };
            _onboarding.Save(_progress);
            _log.LogInfo($"picked up {friend.OwnerName}");
            ShowNextLeg();
            return;
        }

        if (friend.Id != StoryFriends.Annabella.Id)
        {
            // Unscripted person: the log-only fallback, like an unclaimed POI.
            _log.LogInfo(
                $"no pickup for {friend.Name} at {AddressFormatter.Format(friend.Address)}");
            return;
        }

        if (_progress.AnnabellaPickedUp)
        {
            return;
        }

        _progress = _progress with { AnnabellaPickedUp = true };
        _onboarding.Save(_progress);
        _log.LogInfo($"picked up {friend.OwnerName}: both friends aboard");
        // Unity hands the tour to TaskTexts[6] here: drive home for pancakes.
        ShowNextLeg();
    }

    // A shop visit only advances the phase when this visit bought the errand's
    // guaranteed item. The interior passes its own POI, and only the live
    // errand can advance: BACK from any other door is plain navigation, so a
    // later bank or dealer interior cannot complete the story by accident.
    private void LeavePoi(PointOfInterest poi)
    {
        bool purchased = ReferenceEquals(_poi, poi) && BoughtErrandItems(poi);
        _errandStockAtEntry = null;
        if (purchased)
        {
            AdvancePastErrand(poi);
            _host.Hide();
            return;
        }

        _host.ShowScreen(_host.ControlScreen());
    }

    // WHY quantities, not presence: driver.json keeps the trunk between runs,
    // so an old maple stack would otherwise complete the errand the moment the
    // player backs out. Only items added since this door opened count as this
    // visit's purchase; the story then still advances once the player buys
    // more, even when the stack was already there.
    private bool BoughtErrandItems(PointOfInterest poi)
    {
        if (_errandStockAtEntry is not { } before
            || poi.GuaranteedItems is not { Count: > 0 } required
            || !Errand.IsComplete(poi, _trunk))
        {
            return false;
        }

        foreach (string name in required)
        {
            int atEntry = before.TryGetValue(name, out int quantity) ? quantity : 0;
            if (QuantityOf(name) <= atEntry)
            {
                return false;
            }
        }

        return true;
    }

    private Dictionary<string, int> StockOf(PointOfInterest poi)
    {
        var stock = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (poi.GuaranteedItems is { } required)
        {
            foreach (string name in required)
            {
                stock[name] = QuantityOf(name);
            }
        }
        return stock;
    }

    private int QuantityOf(string name)
    {
        foreach (TrunkItem item in _trunk.Items)
        {
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return item.Quantity;
            }
        }
        return 0;
    }

    // The errand closed and the intro is done: the friend tour starts. No
    // marker network exists yet (PORTPLAN 24/59), so the address is the only
    // guidance: the houses are drawn once, banked with the progress, and the
    // HUD walks the legs — Justin, Annabella, home for pancakes, then the
    // community centre.
    private void AdvancePastErrand(PointOfInterest poi)
    {
        _progress = _progress with { MaxErrandDone = true };
        _onboarding.Save(_progress);
        _log.LogInfo($"errand complete: {poi.Name}");
        ShowNextLeg();
        _host.ShowScreen(_host.ControlScreen());
    }

    // Pins the next leg of the tour: the friends first, then grandma's house
    // for pancakes, then the community centre. Clears the line when the tour
    // is over (or the map/profile cannot answer a door). The leg is banked, so
    // a returning player resumes where the saved pickups left them.
    private void ShowNextLeg()
    {
        if (NextFriend() is { } friend)
        {
            Pin(friend, new HudDestinationCopy(HintKey(friend)));
            _log.LogInfo($"friend leg: {friend.Name} at {AddressFormatter.Format(friend.Address)}");
            return;
        }

        if (NextHome() is { } home)
        {
            Pin(home, HomeCopy);
            _log.LogInfo($"home leg: {home.Name} at {AddressFormatter.Format(home.Address)}");
            return;
        }

        if (NextCommunityCentre() is { } communityCentre)
        {
            Pin(communityCentre, CommunityCentreCopy);
            _log.LogInfo(
                $"community centre leg: {communityCentre.Name} at " +
                AddressFormatter.Format(communityCentre.Address));
            return;
        }

        _poi = null;
        _hud?.SetDestination(null);
    }

    private void Pin(PointOfInterest poi, HudDestinationCopy copy)
    {
        _poi = poi;
        Remember(poi);
        _hud?.SetDestination(poi, copy);
    }

    // The pancake leg: grandma's house, drawn from the player's own profile.
    // A save without a matching street degrades to no destination instead of
    // pinning a house that does not exist.
    private PointOfInterest? NextHome()
    {
        if (!_progress.AnnabellaPickedUp || _progress.PancakesDone)
        {
            return null;
        }

        return HomeAddress() is { } address
            ? Place(StoryHome.Definition, address)
            : null;
    }

    // The post-pancake leg: a house on a random street, banked immediately so
    // the same community centre survives a relaunch (Unity saves
    // communityCentreAddress at the start of the introduction).
    private PointOfInterest? NextCommunityCentre()
    {
        if (!_progress.PancakesDone || _progress.CommunityCentreDone)
        {
            return null;
        }

        if (_progress.CommunityCentreAddress is { } stored)
        {
            return Place(StoryCommunityCentre.Definition, stored);
        }

        if (_pois.DrawAddress() is not { } address)
        {
            return null;
        }

        _progress = _progress with { CommunityCentreAddress = address };
        _onboarding.Save(_progress);
        return Place(StoryCommunityCentre.Definition, address);
    }

    private AddressLocation? HomeAddress() =>
        AddressBook.Home(_addressMap, _profile);

    // Banked, not re-drawn: the walk replays on every launch, so a returning
    // player resumes on the leg the saved pickups left them on.
    private PointOfInterest? NextFriend()
    {
        if (_progress.AnnabellaPickedUp)
        {
            return null;
        }

        if (_progress.JustinPickedUp)
        {
            return _progress.AnnabellaAddress is { } stored
                ? Place(StoryFriends.Annabella, stored)
                : null;
        }

        DrawFriendDoors();
        return _progress.JustinAddress is { } justin
            ? Place(StoryFriends.Justin, justin)
            : null;
    }

    // Both friends still live in the street where the player grew up (Unity
    // draws the pair from one street's houses), so both doors are drawn in one
    // go and banked with the progress.
    private void DrawFriendDoors()
    {
        if (_progress.JustinAddress is not null && _progress.AnnabellaAddress is not null)
        {
            return;
        }

        if (FriendStreet() is not { } street)
        {
            return;
        }

        AddressLocation? justin = _progress.JustinAddress;
        if (justin is null)
        {
            justin = _pois.DrawHouse(street, OwnHouse(street));
            if (justin is null)
            {
                return;
            }
        }

        AddressLocation? annabella = _progress.AnnabellaAddress
            ?? _pois.DrawHouse(street, TakenHouses(street, justin));

        _progress = _progress with { JustinAddress = justin, AnnabellaAddress = annabella };
        _onboarding.Save(_progress);
    }

    // The house the player grew up in is never a friend's door (Unity picks the
    // friends' houses off the same street, away from home).
    private int[] OwnHouse(AddressStreet street) =>
        _profile is { } profile
        && string.Equals(profile.StreetId, street.Id, StringComparison.OrdinalIgnoreCase)
            ? [profile.HouseNumber]
            : [];

    // The player's own house plus Justin's door, when his is on this street.
    private int[] TakenHouses(AddressStreet street, AddressLocation? justin)
    {
        int[] taken = OwnHouse(street);
        return justin is { } door && door.StreetName == street.Name
            ? [.. taken, door.HouseNumber]
            : taken;
    }

    // Justin still lives in the street where the player grew up (Unity's task
    // line), so the profile's street wins. A save without one falls back to the
    // first street that has houses, so the tour never dead-ends on a
    // half-filled profile.
    private AddressStreet? FriendStreet()
    {
        AddressStreet? first = null;
        foreach (AddressStreet street in _addressMap.Streets)
        {
            if (HouseNumberPlan.Count(street, _addressMap) <= 0)
            {
                continue;
            }

            if (string.Equals(street.Id, _profile?.StreetId, StringComparison.OrdinalIgnoreCase))
            {
                return street;
            }

            first ??= street;
        }

        return first;
    }

    private static string HintKey(PointOfInterest friend) =>
        friend.Id == StoryFriends.Annabella.Id ? AnnabellaHint : JustinHint;

    private static PointOfInterest Place(PoiDefinition friend, AddressLocation address) =>
        new(friend.Id, friend.Name, address, friend.Type, friend.OwnerName);

    // The addresses page is the discovery list until the marker network lands:
    // every door the story hands over is banked once, and a restart reopens
    // the same list. A duplicate id never rewrites a known address.
    private void Remember(PointOfInterest poi)
    {
        if (_poiDirectory.Add(poi))
        {
            _poiStore.Save(_poiDirectory.All);
        }
    }

    // A save written before the directory file existed still carries the story
    // addresses: rebuild those doors so an upgraded career opens the same
    // addresses page. The current tour leg is pinned again in ResumeTour.
    private void RestoreSavedPois()
    {
        if (_progress.MaxStoreId is { } id
            && _progress.MaxStoreAddress is { } maxStore
            && _pois.Place(id, maxStore) is { } max)
        {
            Remember(max);
        }

        if (_progress.CommunityCentreAddress is { } centre)
        {
            Remember(Place(StoryCommunityCentre.Definition, centre));
        }

        if (_progress.JustinAddress is { } justin)
        {
            Remember(Place(StoryFriends.Justin, justin));
        }

        if (_progress.AnnabellaAddress is { } annabella)
        {
            Remember(Place(StoryFriends.Annabella, annabella));
        }

        if (_profile is not null && HomeAddress() is { } home)
        {
            Remember(Place(StoryHome.Definition, home));
        }
    }

    // A shop purchase moves cash + trunk; the driver save is the one bank
    // both live in (PORTPLAN 51 interim).
    private void OnPoiChanged() => _host.SaveDriver();

    // Preferences page CONTINUE: bank the page, then show the name + address
    // form as the next step of the walk.
    private void ShowOpening()
    {
        SaveStage(OnboardingStage.Opening);
        _host.ShowScreen(OpeningPage());
    }

    // Preferences change: bank the choice, point the resource lookup at the
    // language, and repaint the companion HUD (unit + labels) so the first
    // run never shows a stale language on the one window that is always up.
    private void OnPreferencesChanged(PlayerPreferences preferences)
    {
        _preferences = preferences;
        _preferencesStore.Save(preferences);
        _culture.Apply(preferences.Language);
        _hud?.ApplyPreferences(preferences);
    }

    // Opening screen CONFIRM: the name + address is progress the player just
    // committed, so it banks its own file before the brand page opens. The
    // Steam name only fills an empty profile; a returning player's edited
    // name is never overwritten.
    private void CompleteOpening(PlayerProfile profile)
    {
        _profile = profile;
        _profiles.Save(profile);
        SaveStage(OnboardingStage.Brand);
        _log.LogInfo($"opening confirmed: {profile.DisplayName}, " +
            $"{profile.Country}, {profile.City}, {StreetName(profile.StreetId)} {profile.HouseNumber}");
        _host.ShowScreen(BrandPage());
    }

    private string StreetName(string streetId)
    {
        foreach (AddressStreet street in _addressMap.Streets)
        {
            if (string.Equals(street.Id, streetId, StringComparison.OrdinalIgnoreCase))
            {
                return street.Name;
            }
        }
        return streetId;
    }
}
