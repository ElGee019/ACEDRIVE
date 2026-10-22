using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The best-single-shift world standings block on the shift-ended page
/// (PORTPLAN item 12, Unity ShiftEndedScreen + ShiftStandingsService). Same
/// live mechanism as the XP block on the receipts: the host refreshes before
/// opening the page, so LastResult already carries the fresh or restored
/// result, and the block then follows the backend's updates instead of
/// polling. Nothing known means the —/— placeholder, filled by the event when
/// the board lands. Leaving the page unhooks the subscription (Unity Hide()).
/// </summary>
public sealed class ShiftStandingsBlock : ViewModelBase
{
    private readonly IShiftStandings _board;
    private readonly Action<ShiftStandingsResult> _handler;

    public ShiftStandingsBlock(IShiftStandings board)
    {
        _board = board;
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

    /// <summary>Rank of the board, or the —/— placeholder while nothing is known.</summary>
    public string Line { get => _line; private set => SetField(ref _line, value); }
    private string _line = string.Empty;

    /// <summary>Gained/lost/no-change sentence, empty with no result at all.</summary>
    public string DeltaLine { get => _deltaLine; private set => SetField(ref _deltaLine, value); }
    private string _deltaLine = string.Empty;

    /// <summary>True when the rank moved: drives the teal delta colour.</summary>
    public bool Changed { get => _changed; private set => SetField(ref _changed, value); }
    private bool _changed;

    /// <summary>Pulse trigger for the view: positive restarts the scale
    /// animation, 0 stops and resets it, exactly like the XP block.</summary>
    public int PulseSignal { get => _pulseSignal; private set => SetField(ref _pulseSignal, value); }
    private int _pulseSignal;

    /// <summary>Stops the live updates on the page leave (Unity Hide()).</summary>
    public void Unhook() => _board.ResultUpdated -= _handler;

    private void Apply(ShiftStandingsResult result)
    {
        Line = Strings.Format(
            "ShiftEndedStandings", Ordinals.Of(result.CurrentRank), result.TotalCompetitors);
        DeltaLine = StandingsCopy.Delta(result.RankChange);
        Changed = result.RankChange != 0;
        PulseSignal = Changed ? PulseSignal + 1 : 0;
    }

    private void ShowUnknown()
    {
        Line = Strings.Get("ShiftEndedStandingsUnknown");
        DeltaLine = string.Empty;
        Changed = false;
        PulseSignal = 0;
    }
}
