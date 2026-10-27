using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The OPERATING ZONE EXPANDED page (TOZONES §4.5, Unity ZoneExpansionScreen):
/// the street that just joined, the zone's new count and area, the jobs the
/// next expansion still needs, the world standings of the longest operating
/// zone, and the businesses this growth opened. CONTINUE lands on the page the
/// receipt would have opened; FIND STREET opens the addresses page (the port's
/// map stand-in) whose BACK runs the same continuation. Both actions flash teal
/// on a voice tap and wait out the shared beat, exactly like the receipt pages.
/// </summary>
public sealed class ZoneExpansionScreenViewModel :
    ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;
    private readonly ICommand _findStreetCommand;

    public ZoneExpansionScreenViewModel(
        ZoneExpansion expansion, IReadOnlyList<PointOfInterest> added,
        IZoneStandings standings, VoiceBeat beat,
        ICommand continueCommand, ICommand findStreetCommand)
    {
        ArgumentNullException.ThrowIfNull(expansion);
        ArgumentNullException.ThrowIfNull(added);
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(continueCommand);
        ArgumentNullException.ThrowIfNull(findStreetCommand);
        _beat = beat;
        _continueCommand = continueCommand;
        _findStreetCommand = findStreetCommand;

        ContinueCommand = new RelayCommand(Continue);
        FindStreetCommand = new RelayCommand(FindStreet);
        Title = Strings.Get("ZoneExpansionTitle");
        StreetLine = expansion.StreetName.ToUpperInvariant();
        ZoneLine = Strings.Format("ZoneExpansionZoneLine", expansion.StreetCount);
        PreviousAreaLine = Strings.Format(
            "ZoneExpansionPreviousArea", expansion.PreviousKm);
        NewAreaLine = Strings.Format("ZoneExpansionNewArea", expansion.NewKm);
        JobsToNextLine = Strings.Format(
            "ZoneExpansionJobsToNext", expansion.JobsToNext);
        BusinessLines = BuildBusinessLines(added);
        HasBusinesses = BusinessLines.Count > 0;
        Standings = new ZoneStandingsBlock(standings);
        RebuildVoice();
    }

    /// <summary>"OPERATING ZONE EXPANDED".</summary>
    public string Title { get; }

    /// <summary>The new street's name in the page's caps.</summary>
    public string StreetLine { get; }

    /// <summary>"Operating Zone: 6 streets".</summary>
    public string ZoneLine { get; }

    /// <summary>"Previous Area: 1.9 km".</summary>
    public string PreviousAreaLine { get; }

    /// <summary>"New Total Area: 2.9 km".</summary>
    public string NewAreaLine { get; }

    /// <summary>"Jobs to next expansion: 3".</summary>
    public string JobsToNextLine { get; }

    /// <summary>The businesses this growth opened, in spawn order.</summary>
    public IReadOnlyList<string> BusinessLines { get; }

    /// <summary>False when the expansion opened no business door.</summary>
    public bool HasBusinesses { get; }

    /// <summary>The zone board's rank line, delta line and pulse flag.</summary>
    public ZoneStandingsBlock Standings { get; }

    /// <summary>Hands the player to the page the receipt would have opened.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>Opens the addresses page (the map stand-in).</summary>
    public ICommand FindStreetCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash
    {
        get => _continueFlash;
        private set => SetField(ref _continueFlash, value);
    }
    private bool _continueFlash;

    /// <summary>Teal-glow acknowledgment for the navigating FIND STREET tap.</summary>
    public bool FindStreetFlash
    {
        get => _findStreetFlash;
        private set => SetField(ref _findStreetFlash, value);
    }
    private bool _findStreetFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Continue()
    {
        // Both ways off the page drop the board subscription (receipt
        // precedent): the page is never shown again.
        Standings.Unhook();
        ContinueFlash = false;
        FindStreetFlash = false;
        _continueCommand.Execute(null);
    }

    private void FindStreet()
    {
        Standings.Unhook();
        ContinueFlash = false;
        FindStreetFlash = false;
        _findStreetCommand.Execute(null);
    }

    private void FlashThenContinue()
    {
        ContinueFlash = true;
        _beat.Defer(Continue);
    }

    private void FlashThenFindStreet()
    {
        FindStreetFlash = true;
        _beat.Defer(FindStreet);
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("ZoneExpansionContinue")] = FlashThenContinue,
            [Strings.Get("ZoneExpansionFindStreet")] = FlashThenFindStreet,
        };
    }

    // Copy belongs in resx: the page only hands the formatter the label and
    // the generated name, in the order the settle banked them.
    private static List<string> BuildBusinessLines(
        IReadOnlyList<PointOfInterest> added)
    {
        var lines = new List<string>(added.Count);
        foreach (PointOfInterest poi in added)
        {
            lines.Add(Strings.Format(
                "ZoneExpansionPoiLine", PoiTypeLabels.Of(poi.Type), poi.Name));
        }
        return lines;
    }
}
