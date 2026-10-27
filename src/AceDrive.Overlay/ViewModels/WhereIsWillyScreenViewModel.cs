using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The weekly missing-person quest page (ToWhereIsWilly.md §3.4), the port's
/// stand-in for Unity's map while the search is on (adaptation A1). The phase
/// decides the layout: the search lists the town's coffee shops with Unity's
/// intro sentence and offers GIVE UP once a visit has failed; the found phase
/// names the elder's home address and offers the one DRIVE HOME row. BACK keeps
/// the run alive and hands the player back to the companion.
///
/// The page formats DATA it is handed (the drawn elder, the town, the shops)
/// and never draws, prices or pays anything: every row reports its tap to the
/// flow, which owns the quest.
/// </summary>
public sealed class WhereIsWillyScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly Action<PointOfInterest> _onShop;
    private readonly Action _onHome;
    private readonly Action _onGiveUp;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;
    private readonly string _firstName;

    public WhereIsWillyScreenViewModel(
        WhereIsWillyPhase phase, Resident person, AddressLocation home, string town,
        IReadOnlyList<PointOfInterest> shops, bool showNotHere,
        Action<PointOfInterest> onShop, Action onHome, Action onGiveUp,
        ICommand backCommand, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(shops);
        ArgumentNullException.ThrowIfNull(onShop);
        ArgumentNullException.ThrowIfNull(onHome);
        ArgumentNullException.ThrowIfNull(onGiveUp);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        Phase = phase;
        _onShop = onShop;
        _onHome = onHome;
        _onGiveUp = onGiveUp;
        _backCommand = backCommand;
        _beat = beat;
        _firstName = person.Name.FirstName;
        // WHY BaseName and not Full: a resident's drawn identity can still carry
        // the ride's companion suffix ("and some friends"), and a missing-person
        // sentence about one elder must not claim a whole group vanished.
        IntroLine = Strings.Format("WillyIntro", person.Name.BaseName, _firstName, town);
        FoundLine = Strings.Format("WillyFound", HomeAddress(home, town), _firstName);
        Shops = BuildRows(shops);
        ShowNotHere = showNotHere && IsSearching;
        SelectShopCommand = new ParameterizedCommand<WillyShopRow>(SelectShop);
        DriveHomeCommand = new RelayCommand(DriveHome);
        GiveUpCommand = new RelayCommand(GiveUp);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>The quest's phase this page was built for (search or found).</summary>
    public WhereIsWillyPhase Phase { get; }

    /// <summary>Unity's screen title.</summary>
    public string Title { get; } = Strings.Get("WillyTitle");

    /// <summary>Unity's two-sentence intro, the town and the elder already in it.</summary>
    public string IntroLine { get; }

    /// <summary>Unity's found sentence, naming the short home address.</summary>
    public string FoundLine { get; }

    /// <summary>True for the search layout.</summary>
    public bool IsSearching => Phase == WhereIsWillyPhase.Searching;

    /// <summary>True for the found layout.</summary>
    public bool IsFound => Phase == WhereIsWillyPhase.Found;

    /// <summary>The town's coffee shops, each row pinning one door.</summary>
    public IReadOnlyList<WillyShopRow> Shops { get; }

    /// <summary>The failed-visit hint, shown after at least one wrong shop.</summary>
    public bool ShowNotHere { get; }

    /// <summary>Unity's "They aren't here" sentence.</summary>
    public string NotHereLine { get; } = Strings.Get("WillyNotHere");

    /// <summary>GIVE UP only exists once a visit has failed (Unity's rule).</summary>
    public bool CanGiveUp => IsSearching && ShowNotHere;

    /// <summary>GIVE UP's label.</summary>
    public string GiveUpLabel { get; } = Strings.Get("WillyGiveUp");

    /// <summary>The found page's one row: "DRIVE {first} HOME".</summary>
    public string DriveHomeLabel => Strings.Format("WillyDriveHome", _firstName);

    /// <summary>Pins the tapped coffee shop and closes the page.</summary>
    public ICommand SelectShopCommand { get; }

    /// <summary>Pins the elder's door and closes the page.</summary>
    public ICommand DriveHomeCommand { get; }

    /// <summary>Clears the run: offered only after a failed visit.</summary>
    public ICommand GiveUpCommand { get; }

    /// <summary>Hides the page and keeps the run alive for the calendar row.</summary>
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    /// <summary>Teal-glow acknowledgment for the navigating GIVE UP tap.</summary>
    public bool GiveUpFlash
    {
        get => _giveUpFlash;
        private set => SetField(ref _giveUpFlash, value);
    }
    private bool _giveUpFlash;

    /// <summary>Every row answers to its label; GIVE UP and BACK answer theirs.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void SelectShop(WillyShopRow? row)
    {
        if (row is null)
        {
            return;
        }

        // A click is newer input than a pending voice tap: drop the deferred
        // verb so the page cannot act twice.
        _beat.Cancel();
        GiveUpFlash = false;
        _onShop(row.Poi);
    }

    private void DriveHome()
    {
        _beat.Cancel();
        BackFlash = false;
        _onHome();
    }

    private void GiveUp()
    {
        GiveUpFlash = false;
        _onGiveUp();
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out.
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private static List<WillyShopRow> BuildRows(IReadOnlyList<PointOfInterest> shops)
    {
        List<WillyShopRow> rows = new(shops.Count);
        foreach (PointOfInterest poi in shops)
        {
            rows.Add(new WillyShopRow(poi, poi.Name, AddressFormatter.Format(poi.Address)));
        }
        return rows;
    }

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>();
        foreach (WillyShopRow row in Shops)
        {
            actions[row.Name] = () => SelectShop(row);
        }
        if (IsFound)
        {
            actions[DriveHomeLabel] = DriveHome;
        }
        if (CanGiveUp)
        {
            actions[GiveUpLabel] = () =>
            {
                GiveUpFlash = true;
                _beat.Defer(GiveUp);
            };
        }

        // Back navigates: glow first, act at the end of the shared beat.
        actions[Strings.Get("WillyBack")] = () =>
        {
            BackFlash = true;
            _beat.Defer(Back);
        };
        VoiceActions = actions;
    }

    // The port's one address shape with the city appended, the same pair the
    // companion prints (adaptation A2).
    private static string HomeAddress(AddressLocation home, string town) =>
        Strings.Format("HudAddressWithCity", AddressFormatter.Format(home), town);
}
