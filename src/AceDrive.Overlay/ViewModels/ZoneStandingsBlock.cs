using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The operating-zone world-standings block on the expansion page (TOZONES
/// §4.5, Unity ZoneExpansionScreen + ZoneStandingsService). Same live mechanism
/// as the receipt and shift-ended blocks: the host refreshes against the new
/// area before opening the page, so LastResult already carries the fresh
/// result, and the block then follows the backend's updates instead of
/// polling. Nothing known means the —/— placeholder, filled by the event when
/// the board lands. Leaving the page unhooks the subscription (Unity Hide()).
/// </summary>
public sealed class ZoneStandingsBlock : ViewModelBase
{
    private readonly IZoneStandings _board;
    private readonly Action<ZoneStandingsResult> _handler;

    public ZoneStandingsBlock(IZoneStandings board)
    {
        _board = board;
        IsVisible = board.IsAvailable;
        if (_board.LastResult is { } known)
        {
            Apply(known);
        }
        else
        {
            ShowUnknown();
        }
        _handler = Apply;
        _board.ResultUpdated += _handler;
    }

    /// <summary>
    /// False when the build has no board behind it: the page hides the whole
    /// block instead of printing a placeholder nobody will ever fill.
    /// </summary>
    public bool IsVisible { get; }

    /// <summary>Rank of the board, or the —/— placeholder while nothing is known.</summary>
    public string Line { get => _line; private set => SetField(ref _line, value); }
    private string _line = string.Empty;

    /// <summary>Gained/lost/no-change sentence, empty with no result yet.</summary>
    public string DeltaLine { get => _deltaLine; private set => SetField(ref _deltaLine, value); }
    private string _deltaLine = string.Empty;

    /// <summary>True when the rank moved: drives the teal delta colour.</summary>
    public bool Changed { get => _changed; private set => SetField(ref _changed, value); }
    private bool _changed;

    /// <summary>Pulse trigger for the view: positive restarts the scale
    /// animation, 0 stops and resets it, exactly like the receipt blocks.</summary>
    public int PulseSignal { get => _pulseSignal; private set => SetField(ref _pulseSignal, value); }
    private int _pulseSignal;

    /// <summary>Stops the live updates on the page leave (Unity Hide()).</summary>
    public void Unhook() => _board.ResultUpdated -= _handler;

    // Unity's ApplyStandings: the rank line is the resx pattern, the delta is
    // the shared gained/lost copy, and a first-ever result shows no delta at
    // all (there is no earlier rank to have moved from).
    private void Apply(ZoneStandingsResult result)
    {
        Line = Strings.Format(
            "ZoneStandingsRank", Ordinals.Of(result.CurrentRank), result.TotalCompetitors);
        DeltaLine = result.IsFirstTime ? string.Empty : StandingsCopy.Delta(result.RankChange);
        Changed = result.RankChange != 0;
        PulseSignal = Changed ? PulseSignal + 1 : 0;
    }

    private void ShowUnknown()
    {
        Line = Strings.Get("ZoneStandingsUnknown");
        DeltaLine = string.Empty;
        Changed = false;
        PulseSignal = 0;
    }
}
