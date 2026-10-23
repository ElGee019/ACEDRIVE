using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The OPEN NEW CAREER page (PORTPLAN item 18, port of Unity
/// DeliveryHubScreen): pick a tier, pick one hub in a career list and
/// spend CredP on OPEN CAREER. Each career title is itself a button that
/// picks the list's next buyable hub (the narrow overlay stacks the lists
/// vertically instead of Unity's side-by-side columns). Buying a hub
/// connects that category's stores to the delivery board — Unity's map
/// backfill, ported as the career's eligibility rule — and hands the new
/// owned record to the host, which places the hub's pickup door before the
/// stores draw theirs. There is no confirmation dialog: the enabled/disabled
/// state of OPEN CAREER is the whole confirm (Unity HasSelectedCareer). Built
/// fresh on every visit, like Unity's re-show: tier 1 active, nothing
/// selected.
/// Voice parity with the board: every button answers to its text.
/// </summary>
public sealed class DeliveryHubScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    // Unity's hand-built screen pairs careers per tier; the odd one out
    // stays a COMING SOON placeholder. AceDrive stacks the lists
    // vertically, but keeps Unity's two-slot padding per tier.
    private const int ColumnCount = 2;

    private readonly DeliveryCareer _career;
    private readonly DeliveryCatalog _board;
    private readonly CityEconomy _city;
    private readonly ILog _log;
    private readonly VoiceBeat _beat;
    private readonly Action _goBack;
    // The host's map side of a purchase: the hub's pickup door is placed
    // here, before the backfill stores draw their own free addresses.
    private readonly Action<OwnedHub>? _hubOpened;
    private readonly RelayCommand _openCareer;
    private int _tierIndex;
    private int _selectedCareer = -1;
    private int _selectedLevel = -1;

    public DeliveryHubScreenViewModel(
        DeliveryCareer career, DeliveryCatalog board, CityEconomy city, ILog log,
        ICommand backCommand,
        VoiceBeat beat,
        Action<OwnedHub>? hubOpened = null)
    {
        _career = career;
        _board = board;
        _city = city;
        _log = log;
        _beat = beat;
        _hubOpened = hubOpened;
        _goBack = () => backCommand.Execute(null);

        ChooseTierCommand = new ParameterizedCommand<HubTierCell>(ChooseTier);
        ChooseCareerCommand = new ParameterizedCommand<HubCell>(ChooseCareer);
        ChooseColumnCommand = new ParameterizedCommand<HubColumn>(ChooseColumn);
        _openCareer = new RelayCommand(OpenCareer, HasSelection);
        OpenCareerCommand = _openCareer;
        BackCommand = new RelayCommand(_goBack);
        Rebuild();
    }

    public IReadOnlyList<HubTierCell> TierCells { get => _tierCells; private set => SetField(ref _tierCells, value); }
    private IReadOnlyList<HubTierCell> _tierCells = [];

    public IReadOnlyList<HubColumn> Columns { get => _columns; private set => SetField(ref _columns, value); }
    private IReadOnlyList<HubColumn> _columns = [];

    /// <summary>Unity HasSelectedCareer: one career picked right now. Drives
    /// the OPEN CAREER command state and, through it, the view showing the
    /// button at all (hidden until a pick exists).</summary>
    public bool HasSelectedCareer { get => _hasSelectedCareer; private set => SetField(ref _hasSelectedCareer, value); }
    private bool _hasSelectedCareer;

    public string CredPLine { get => _credPLine; private set => SetField(ref _credPLine, value); }
    private string _credPLine = "";

    public string CityGrowthLine { get => _cityGrowthLine; private set => SetField(ref _cityGrowthLine, value); }
    private string _cityGrowthLine = "";

    public ICommand ChooseTierCommand { get; }
    public ICommand ChooseCareerCommand { get; }
    public ICommand ChooseColumnCommand { get; }
    public ICommand OpenCareerCommand { get; }
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for a voice tap on BACK.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void ChooseTier(HubTierCell? cell)
    {
        if (cell is null) return;
        _tierIndex = cell.Index;
        ClearSelection();
        Rebuild();
    }

    private void ChooseCareer(HubCell? cell)
    {
        // Unity: only an unowned, affordable hub accepts the click.
        if (cell is null || !cell.IsSelectable) return;
        _selectedCareer = cell.CareerIndex;
        _selectedLevel = cell.LevelIndex;
        Rebuild();
    }

    // The title button's shortcut: pick the list's first buyable hub, so
    // "COFFEE BEANS" alone prepares the next purchase in that career. With
    // nothing buyable the button is disabled (HubColumn.CanPick) and a
    // stale call just no-ops here.
    private void ChooseColumn(HubColumn? column)
    {
        if (column is null) return;
        foreach (HubCell cell in column.Cells)
        {
            if (cell.IsSelectable)
            {
                ChooseCareer(cell);
                return;
            }
        }
    }

    // Unity HasSelectedCareer drives the OPEN CAREER button state; the
    // purchase itself re-checks through the career so a stale cell can
    // never spend CredP twice.
    private bool HasSelection() =>
        _selectedCareer >= 0
        && _tierIndex < _career.Hubs.Tiers.Count
        && _career.CanBuy(_career.Hubs.Tiers[_tierIndex].Id, _selectedCareer, _selectedLevel);

    private void OpenCareer()
    {
        if (!HasSelection()) return;
        HubTier tier = _career.Hubs.Tiers[_tierIndex];
        HubCareer chosen = tier.Careers[_selectedCareer];
        string name = chosen.Levels[_selectedLevel].Name;
        if (!_career.TryOpenCareer(tier.Id, _selectedCareer, _selectedLevel)) return;

        _log.LogInfo($"career opened: {name} ({chosen.Id}) CredP left {_career.CredP:0}");
        // The newly owned record is the one just appended: the host pins its
        // pickup door before any backfilled store draws an address.
        _hubOpened?.Invoke(_career.Owned[^1]);
        ClearSelection();
        Rebuild();
    }

    private void ClearSelection()
    {
        _selectedCareer = -1;
        _selectedLevel = -1;
    }

    // One redraw for every state change, like Unity's Refresh* chain:
    // header numbers, tier row, career columns, button state, voice labels.
    private void Rebuild()
    {
        IReadOnlyList<HubTier> tiers = _career.Hubs.Tiers;
        var tierCells = new List<HubTierCell>(tiers.Count);
        for (int i = 0; i < tiers.Count; i++)
        {
            tierCells.Add(new HubTierCell(i, Strings.Format("HubTier", tiers[i].Id), i == _tierIndex));
        }
        TierCells = tierCells;

        var columns = new List<HubColumn>(ColumnCount);
        if (_tierIndex < tiers.Count)
        {
            HubTier tier = tiers[_tierIndex];
            for (int c = 0; c < tier.Careers.Count && c < ColumnCount; c++)
            {
                columns.Add(BuildColumn(tier.Careers[c], c));
            }
        }
        while (columns.Count < ColumnCount)
        {
            columns.Add(new HubColumn(columns.Count, Strings.Get("HubComingSoon"), [], false, false));
        }
        Columns = columns;

        CredPLine = Strings.Format("DeliveryCredP", _career.CredP);
        // The headline follows the connected stores, on the town's live stock.
        CityGrowthLine = DeliveryCopy.CityGrowth(
            _city.TotalGrowthPerHour(_career.VisibleStores(_board)));
        HasSelectedCareer = HasSelection();
        _openCareer.RaiseCanExecuteChanged();
        RebuildVoice();
    }

    private HubColumn BuildColumn(HubCareer career, int careerIndex)
    {
        var cells = new List<HubCell>(career.Levels.Count);
        for (int level = 0; level < career.Levels.Count; level++)
        {
            HubLevel hub = career.Levels[level];
            bool owned = _career.IsOwned(hub.Name);
            bool affordable = _career.CredP >= hub.Cost;
            bool selected = careerIndex == _selectedCareer && level == _selectedLevel
                && !owned && affordable;
            cells.Add(new HubCell(
                careerIndex, level,
                owned ? hub.Name : Strings.Format("HubPrice", hub.Name, hub.Cost),
                hub.Name,
                owned ? "" : Strings.Format("HubCellPrice", hub.Cost),
                owned, affordable, selected));
        }
        // _selectedCareer is -1 while nothing is picked, so no list is
        // active on a fresh page; a pick in another list drops this flag
        // with the same rebuild that moves the cell highlight.
        return new HubColumn(
            careerIndex, career.Label, cells,
            cells.Any(c => c.IsSelectable), careerIndex == _selectedCareer);
    }

    private void RebuildVoice()
    {
        // Back is the page's one navigating voice tap: it flashes teal
        // first and the page waits out the shared beat, so the glow shows
        // before the flip. Picks and purchases stay on this page and act at
        // once like a click.
        var actions = new Dictionary<string, Action>
        {
            [Strings.Get("HubOpenCareer")] = OpenCareer,
            [Strings.Get("DeliveryBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(() =>
                {
                    BackFlash = false;
                    _goBack();
                });
            },
        };
        foreach (HubTierCell tier in TierCells)
        {
            HubTierCell hit = tier;
            actions[hit.LabelLine] = () => ChooseTier(hit);
        }
        foreach (HubColumn column in Columns)
        {
            // The title button's voice twin: "COFFEE BEANS" answers with the
            // same shortcut as a click. COMING SOON fillers are disabled
            // buttons, so they stay voiceless like the other no-ops.
            HubColumn hitColumn = column;
            if (hitColumn.CanPick)
            {
                actions[hitColumn.TitleLine] = () => ChooseColumn(hitColumn);
            }
            foreach (HubCell cell in column.Cells)
            {
                HubCell hit = cell;
                actions[hit.LabelLine] = () => ChooseCareer(hit);
            }
        }
        VoiceActions = actions;
    }
}
