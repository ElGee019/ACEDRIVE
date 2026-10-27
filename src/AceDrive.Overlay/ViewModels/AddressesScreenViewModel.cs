using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The addresses page (PORTPLAN 24/27 + TOZONES §3.5): the operating zone's
/// readout — street count, area, jobs to the next expansion and the streets it
/// covers — then every discovered POI, alphabetical by name, with the street
/// and house number under it. This is the temporary stand-in for markers on
/// the map: tapping an address pins the HUD's drive-to line, and driving there
/// puts ENTER up on the companion so the place can be entered. The page writes
/// nothing itself — the HUD owns the destination and the host owns navigation.
/// </summary>
public sealed class AddressesScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly PoiDirectory _directory;
    private readonly ITelemetryHud? _hud;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;
    private readonly Action? _onPinned;
    private string? _selectedId;

    public AddressesScreenViewModel(
        PoiDirectory directory, OperatingZone zone, ITelemetryHud? hud, ICommand backCommand,
        VoiceBeat beat, Action? onPinned = null)
    {
        ArgumentNullException.ThrowIfNull(zone);
        _directory = directory;
        _hud = hud;
        _backCommand = backCommand;
        _beat = beat;
        _onPinned = onPinned;
        SelectCommand = new ParameterizedCommand<AddressRow>(Select);
        BackCommand = new RelayCommand(Back);
        // The zone readout (TOZONES §3.5): Unity paints these facts on the map
        // HUD, and this page is the port's stand-in for that map. The zone's
        // cached ZoneMap is the addressable area every draw already reads, so
        // the header can never name a street the player has not discovered.
        StreetNames = BuildStreetNames(zone);
        ZoneLine = Strings.Format("AddressesZoneLine", StreetNames.Count, zone.Km);
        // A zone that already covers every street on the map has nothing left
        // to buy: the counter stays due (Unity's empty-pool path), so the page
        // names the completion instead of promising jobs for no expansion.
        NextExpansionLine = zone.NextStreet is null
            ? Strings.Get("AddressesZoneComplete")
            : Strings.Format("AddressesZoneNext", zone.JobsUntilNext);
        Rows = BuildRows();
        RebuildVoice();
    }

    /// <summary>Pins the HUD's drive-to line for the tapped row.</summary>
    public ICommand SelectCommand { get; }

    /// <summary>The page's way back to the home page it was opened from.</summary>
    public ICommand BackCommand { get; }

    /// <summary>The discovered POIs, sorted by display name.</summary>
    public IReadOnlyList<AddressRow> Rows { get => _rows; private set => SetField(ref _rows, value); }
    private IReadOnlyList<AddressRow> _rows = [];

    /// <summary>True when no POI has been discovered yet.</summary>
    public bool IsEmpty => Rows.Count == 0;

    /// <summary>The list's inverse flag, so the view can hide the caption on an empty page.</summary>
    public bool HasRows => !IsEmpty;

    /// <summary>The empty-page hint, so the page never reads as broken.</summary>
    public string EmptyLine { get; } = Strings.Get("AddressesEmpty");

    /// <summary>Caption above the list.</summary>
    public string SelectLabel { get; } = Strings.Get("AddressesSelect");

    /// <summary>"Operating Zone: 6 streets · 2.9 km": Unity's map HUD line, which
    /// this page carries because the port has no map yet.</summary>
    public string ZoneLine { get; }

    /// <summary>"Jobs to next expansion: 3", or the completion line once the
    /// zone covers every street on the map.</summary>
    public string NextExpansionLine { get; }

    /// <summary>Caption above the discovered street names.</summary>
    public string StreetsLabel { get; } = Strings.Get("AddressesStreetsLabel");

    /// <summary>The streets the zone covers, home street first: the addressable
    /// area every new draw reads, so a door can never sit off-zone.</summary>
    public IReadOnlyList<string> StreetNames { get; }

    /// <summary>The confirmation under the list once an address is picked.</summary>
    public string SelectedLine { get => _selectedLine; private set => SetField(ref _selectedLine, value); }
    private string _selectedLine = "";

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // A click and a voice tap both pin at once: the page stays open, so there
    // is no navigation beat to wait for. The selected row stays teal, which is
    // the acknowledgment itself.
    private void Select(AddressRow? row)
    {
        if (row is null)
        {
            return;
        }

        // A click is newer input than a pending voice BACK: drop its deferred
        // navigation and the stale glow so the page cannot flip afterwards.
        _beat.Cancel();
        BackFlash = false;
        _selectedId = row.Poi.Id;
        _hud?.SetDestination(row.Poi);
        // WHY: the destination has to be readable the moment it is pinned, not
        // only after the player closes this page; the companion surfaces its
        // task text over the still-open panel.
        _hud?.ShowTaskText();
        // The pin is the "set a destination yourself" action the ToDo subject
        // counts (Unity manuallySetDestinations); the host does the counting
        // and the sync, the page only reports the tap.
        _onPinned?.Invoke();
        Rows = BuildRows();
        SelectedLine = Strings.Format("AddressesSelected", row.Name, row.Address);
    }

    /// <summary>Builds the street rows from the zone's cached addressable map.</summary>
    private static List<string> BuildStreetNames(OperatingZone zone)
    {
        // The count and the names come from the same rows on purpose: a legacy
        // save can carry more street ids than the map can name (the old "s1",
        // "s2" pool), and the header must never claim a street it cannot list.
        IReadOnlyList<AddressStreet> streets = zone.ZoneMap.Streets;
        var names = new List<string>(streets.Count);
        foreach (AddressStreet street in streets)
        {
            names.Add(street.Name);
        }
        return names;
    }

    private List<AddressRow> BuildRows()
    {
        IReadOnlyList<PointOfInterest> pois = _directory.Alphabetical();
        var rows = new List<AddressRow>(pois.Count);
        foreach (PointOfInterest poi in pois)
        {
            rows.Add(new AddressRow(
                poi,
                poi.Name,
                AddressFormatter.Format(poi.Address),
                string.Equals(poi.Id, _selectedId, StringComparison.Ordinal)));
        }
        return rows;
    }

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>();
        foreach (AddressRow row in Rows)
        {
            // A row's spoken name pins it exactly like a click.
            actions[row.Name] = () => Select(row);
        }

        // Back navigates: glow first, act at the end of the shared beat.
        actions[Strings.Get("AddressesBack")] = () =>
        {
            BackFlash = true;
            _beat.Defer(Back);
        };
        VoiceActions = actions;
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out.
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }
}
