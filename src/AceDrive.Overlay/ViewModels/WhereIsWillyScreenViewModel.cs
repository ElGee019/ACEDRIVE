using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The weekly missing-person quest page (ToWhereIsWilly.md Â§3.4), the port's
/// stand-in for Unity's map while the search runs (adaptation A1): Unity's
/// intro sentence and the town's coffee shops, with GIVE UP once a visit has
/// failed. The found step is not a page â€” the companion announces it and its
/// DRIVE button (HudTaskPrompt) â€” so this page only ever paints the search.
/// BACK is its one exit: on the untouched page the flow declines the run and
/// lands back on the calendar, once a shop is pinned it only hides the page.
///
/// The page formats DATA it is handed (the drawn elder, the town, the shops)
/// and never draws, prices or pays anything: every row reports its tap to the
/// flow, which owns the quest.
/// </summary>
public sealed class WhereIsWillyScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly Action<PointOfInterest> _onShop;
    private readonly Action _onGiveUp;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public WhereIsWillyScreenViewModel(
        Resident person, string town, IReadOnlyList<PointOfInterest> shops,
        bool showNotHere, Action<PointOfInterest> onShop, Action onGiveUp,
        ICommand backCommand, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(shops);
        ArgumentNullException.ThrowIfNull(onShop);
        ArgumentNullException.ThrowIfNull(onGiveUp);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        _onShop = onShop;
        _onGiveUp = onGiveUp;
        _backCommand = backCommand;
        _beat = beat;
        // WHY BaseName and not Full: a resident's drawn identity can still carry
        // the ride's companion suffix ("and some friends"), and a missing-person
        // sentence about one elder must not claim a whole group vanished.
        IntroLine = Strings.Format("WillyIntro", person.Name.BaseName, person.Name.FirstName, town);
        Shops = BuildRows(shops);
        ShowNotHere = showNotHere;
        SelectShopCommand = new ParameterizedCommand<WillyShopRow>(SelectShop);
        GiveUpCommand = new RelayCommand(GiveUp);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>Unity's screen title.</summary>
    public string Title { get; } = Strings.Get("WillyTitle");

    /// <summary>Unity's two-sentence intro, the town and the elder already in it.</summary>
    public string IntroLine { get; }

    /// <summary>The town's coffee shops, each row pinning one door.</summary>
    public IReadOnlyList<WillyShopRow> Shops { get; }

    /// <summary>The failed-visit hint, shown after at least one wrong shop.</summary>
    public bool ShowNotHere { get; }

    /// <summary>Unity's "They aren't here" sentence.</summary>
    public string NotHereLine { get; } = Strings.Get("WillyNotHere");

    /// <summary>GIVE UP only exists once a visit has failed (Unity's rule).</summary>
    public bool CanGiveUp => ShowNotHere;

    /// <summary>GIVE UP's label.</summary>
    public string GiveUpLabel { get; } = Strings.Get("WillyGiveUp");

    /// <summary>Pins the tapped coffee shop and closes the page.</summary>
    public ICommand SelectShopCommand { get; }

    /// <summary>Clears the run: offered only after a failed visit.</summary>
    public ICommand GiveUpCommand { get; }

    /// <summary>The page's way out: the flow decides between declining the run
    /// (search page, before a shop tap) and leaving it running.</summary>
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
}
