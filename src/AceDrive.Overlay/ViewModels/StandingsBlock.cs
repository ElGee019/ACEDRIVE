using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The XP world standings block any receipt can host (Unity's rivals block
/// on TaxiReceiptScreen + DeliveryReceiptScreen, PORTPLAN item 60). Reads
/// the board once on construct — the host refreshes before opening the
/// receipt, so LastResult already carries the fresh or restored result —
/// then follows the backend's live updates, never polling. Nothing known
/// means the —/— placeholder, filled by the event when a board lands.
/// Leaving the page unhooks the subscription (Unity Hide()).
/// </summary>
public sealed class StandingsBlock : ViewModelBase
{
    private readonly IRivalsService _rivals;
    private readonly Action<RivalsResult> _handler;

    public StandingsBlock(IRivalsService rivals)
    {
        _rivals = rivals;
        if (_rivals.LastResult is { } known)
        {
            Apply(known);
        }
        else
        {
            ShowUnknown();
        }
        _handler = Apply;
        _rivals.ResultUpdated += _handler;
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

    /// <summary>
    /// Pulse trigger for the view: positive restarts the scale animation,
    /// 0 stops and resets it. Unity stops any running coroutine and resets
    /// the scales on every result, then starts a new pulse only when the
    /// rank moved — so each fresh result gets a new positive value and a
    /// no-change result cancels an in-flight pulse.
    /// </summary>
    public int PulseSignal { get => _pulseSignal; private set => SetField(ref _pulseSignal, value); }
    private int _pulseSignal;

    /// <summary>Stops the live updates on the page leave (Unity Hide()).</summary>
    public void Unhook() => _rivals.ResultUpdated -= _handler;

    // Unity rivals block, sentence assembly split per AGENTS.md: the Core
    // gives numbers, the resx gives copy. Rank changes of any size flip
    // Changed (delta colour) and advance PulseSignal (scale pulse).
    private void Apply(RivalsResult result)
    {
        Line = Strings.Format(
            "ReceiptStandings", Ordinals.Of(result.CurrentRank), result.TotalCompetitors);
        DeltaLine = result.RankChange switch
        {
            1 => Strings.Get("ReceiptStandingsGainedOne"),
            > 0 => Strings.Format("ReceiptStandingsGained", result.RankChange),
            -1 => Strings.Get("ReceiptStandingsLostOne"),
            < 0 => Strings.Format("ReceiptStandingsLost", -result.RankChange),
            _ => Strings.Get("ReceiptStandingsNoChange"),
        };
        Changed = result.RankChange != 0;
        PulseSignal = Changed ? PulseSignal + 1 : 0;
    }

    // Unity's placeholder branch: no board, no snapshot — —/— with an
    // empty delta line, until the event brings a real result.
    private void ShowUnknown()
    {
        Line = Strings.Get("ReceiptStandingsUnknown");
        DeltaLine = string.Empty;
        Changed = false;
        PulseSignal = 0;
    }
}
