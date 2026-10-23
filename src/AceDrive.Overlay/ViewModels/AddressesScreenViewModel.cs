using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The addresses page (PORTPLAN 24/27): every discovered POI, alphabetical by
/// name, with the street and house number under it. This is the temporary
/// stand-in for markers on the map: tapping an address pins the HUD's
/// drive-to line, and driving there puts ENTER up on the companion so the
/// place can be entered. The page writes nothing itself — the HUD owns the
/// destination and the host owns navigation.
/// </summary>
public sealed class AddressesScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    private readonly PoiDirectory _directory;
    private readonly ITelemetryHud? _hud;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;
    private string? _selectedId;

    public AddressesScreenViewModel(
        PoiDirectory directory, ITelemetryHud? hud, ICommand backCommand, VoiceBeat beat)
    {
        _directory = directory;
        _hud = hud;
        _backCommand = backCommand;
        _beat = beat;
        SelectCommand = new ParameterizedCommand<AddressRow>(Select);
        BackCommand = new RelayCommand(Back);
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
        Rows = BuildRows();
        SelectedLine = Strings.Format("AddressesSelected", row.Name, row.Address);
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
