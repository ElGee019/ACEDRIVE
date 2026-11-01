using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// A POI family's explanation page (TOZONES §4.5): the operating-zone
/// expansion page's CONTINUE opens it before the page the receipt would have
/// shown, so a business is explained the moment its door appears — every
/// time, not only the first one of the career. The insurance broker's first
/// door is the exception: the first crash repair opens the door, and that
/// moment opens this page directly. Same skeleton as the calendar briefings:
/// title band, body copy, one forward verb. CONTINUE runs the continuation;
/// the page paints copy and decides nothing.
///
/// It keeps the panel open (<see cref="IKeepPanelOpen"/>): the body outlives
/// the 15 s idle window, and a reader hidden mid-page would have to reopen
/// the expansion to see it again.
/// </summary>
public sealed class PoiIntroScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly Action _onContinue;
    private readonly VoiceBeat _beat;

    public PoiIntroScreenViewModel(PoiIntro intro, Action onContinue, VoiceBeat beat)
    {
        ArgumentNullException.ThrowIfNull(intro);
        ArgumentNullException.ThrowIfNull(onContinue);
        ArgumentNullException.ThrowIfNull(beat);

        Title = intro.Title;
        Body = intro.Body;
        _onContinue = onContinue;
        _beat = beat;
        ContinueCommand = new RelayCommand(Continue);
        RebuildVoice();
    }

    /// <summary>The family in the plural, e.g. "Insurance Brokers".</summary>
    public string Title { get; }

    /// <summary>The explanation: what the place is for and how to use it.</summary>
    public string Body { get; }

    /// <summary>The page's one forward verb.</summary>
    public ICommand ContinueCommand { get; }

    /// <summary>CONTINUE's label, spoken as well as painted.</summary>
    public string ContinueLabel { get; } = Strings.Get("PoiIntroContinue");

    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash
    {
        get => _continueFlash;
        private set => SetField(ref _continueFlash, value);
    }
    private bool _continueFlash;

    /// <summary>CONTINUE answers its label.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Continue()
    {
        // A click is newer input than a pending voice tap: drop the deferred
        // verb so the continuation cannot be asked to run twice.
        ContinueFlash = false;
        _beat.Cancel();
        _onContinue();
    }

    private void RebuildVoice()
    {
        // The one verb navigates: glow first, act at the end of the shared
        // beat, like every other page's buttons.
        VoiceActions = new Dictionary<string, Action>
        {
            [ContinueLabel] = () =>
            {
                ContinueFlash = true;
                _beat.Defer(Continue);
            },
        };
    }
}
