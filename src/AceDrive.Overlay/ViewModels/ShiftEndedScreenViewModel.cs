using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Shift-ended page (PORTPLAN item 12, port of Unity ShiftEndedScreen): the
/// shift that just timed out — jobs, earnings, duration — its lifetime
/// records and the best-single-shift world standings. Unity chains this page
/// before the map, so a shift is never silently over; the host has already
/// banked the record and pushed it by the time the page opens, so this page
/// only reports. CONTINUE returns to the control page and the next TAXI tap
/// opens a fresh shift, like Unity's doorway back to the map.
/// </summary>
public sealed class ShiftEndedScreenViewModel :
    ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;

    public ShiftEndedScreenViewModel(
        ShiftSummary summary, ShiftRecord record, IShiftStandings standings,
        VoiceBeat beat, ICommand continueCommand)
    {
        _beat = beat;
        _continueCommand = continueCommand;
        ContinueCommand = new RelayCommand(Continue);
        JobsLine = Strings.Format("ShiftEndedJobs", summary.Jobs);
        EarningsLine = Strings.Format("ShiftEndedEarnings", summary.Earnings);
        DurationLine = Strings.Format("ShiftEndedDuration", summary.DurationMinutes);
        LongestLine = Strings.Format("ShiftEndedLongest", record.LongestJobs);
        BestLine = Strings.Format("ShiftEndedBest", record.BestEarnings);
        TotalShiftsLine = Strings.Format("ShiftEndedTotal", record.ShiftsCompleted);
        Standings = new ShiftStandingsBlock(standings);
        RebuildVoice();
    }

    /// <summary>Jobs completed in the shift that just ended.</summary>
    public string JobsLine { get; }

    /// <summary>What those jobs paid (the shift total, not the wallet).</summary>
    public string EarningsLine { get; }

    /// <summary>First start to the last dropoff, in whole minutes.</summary>
    public string DurationLine { get; }

    /// <summary>Lifetime record: most jobs in one shift (Unity longestShiftJobCount).</summary>
    public string LongestLine { get; }

    /// <summary>Lifetime record: best single-shift earnings.</summary>
    public string BestLine { get; }

    /// <summary>Lifetime count of finished shifts.</summary>
    public string TotalShiftsLine { get; }

    /// <summary>The shift board's rank line, delta line and pulse flag.</summary>
    public ShiftStandingsBlock Standings { get; }

    public ICommand ContinueCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash { get => _continueFlash; private set => SetField(ref _continueFlash, value); }
    private bool _continueFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the board subscription dies with the page (receipt precedent).
    private void Continue()
    {
        Standings.Unhook();
        ContinueFlash = false;
        _continueCommand.Execute(null);
    }

    private void FlashThenContinue()
    {
        ContinueFlash = true;
        _beat.Defer(Continue);
    }

    private void RebuildVoice() =>
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("ReceiptContinue")] = FlashThenContinue,
        };
}
