using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// One dialogue, one entry per page: the birthday gift-handover (Career Start
/// item 4) and later story beats such as grandma's pancakes. The caller owns
/// what finishing means, so the same screen serves every conversation.
/// </summary>
public sealed class DialogueScreenViewModel : ViewModelBase, IVoiceScreen, IDialogueScreen
{
    private readonly Action _onFinished;
    private readonly VoiceBeat _beat;
    private readonly IReadOnlyList<DialogueLine> _lines;
    private int _index;

    public DialogueScreenViewModel(
        IReadOnlyList<DialogueLine> lines, string title, VoiceBeat beat, Action onFinished)
    {
        _onFinished = onFinished;
        _beat = beat;
        _lines = lines;
        Title = title;
        ContinueCommand = new RelayCommand(Continue);
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("DialogueContinue")] = VoiceContinue,
        };
        Refresh();
    }

    /// <summary>Page heading, owned by the conversation that opened the screen.</summary>
    public string Title { get; }

    public string SpeakerName { get => _speaker; private set => SetField(ref _speaker, value); }
    private string _speaker = "";

    public string Body { get => _body; private set => SetField(ref _body, value); }
    private string _body = "";

    public ICommand ContinueCommand { get; }

    /// <summary>Spoken label -&gt; action, for the coming voice phase (buttons stay mouse-first).</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    /// <summary>True once the last page has been acknowledged (idle-hide resumes).</summary>
    public bool IsComplete { get; private set; }

    /// <summary>Teal-glow acknowledgment for a voice tap on CONTINUE.</summary>
    public bool ContinueFlash { get => _continueFlash; private set => SetField(ref _continueFlash, value); }
    private bool _continueFlash;

    private void Continue()
    {
        // A click is newer input: drop a pending voice tap and its glow so the
        // beat cannot turn a second page afterwards.
        _beat.Cancel();
        ContinueFlash = false;
        Advance();
    }

    // Every page's voice Continue gets the same acknowledgment as the other
    // screens: teal text for the shared half-second, then the turn or the
    // finish. Clicks stay instant with no glow.
    private void VoiceContinue()
    {
        ContinueFlash = true;
        _beat.Defer(() =>
        {
            ContinueFlash = false;
            Advance();
        });
    }

    private void Advance()
    {
        if (_index + 1 < _lines.Count)
        {
            _index++;
            Refresh();
        }
        else
        {
            Finish();
        }
    }

    private void Finish()
    {
        IsComplete = true;
        _onFinished();
    }

    private void Refresh()
    {
        if (_lines.Count == 0)
        {
            SpeakerName = "";
            Body = "";
            return;
        }

        DialogueLine line = _lines[Math.Min(_index, _lines.Count - 1)];
        SpeakerName = line.Speaker switch
        {
            "GRANDMA" => Strings.Get("SpeakerGrandma"),
            "YOU" => Strings.Get("SpeakerYou"),
            "ANNABELLA" => Strings.Get("SpeakerAnnabella"),
            "JUSTIN" => Strings.Get("SpeakerJustin"),
            "SIGNORE MORO" => Strings.Get("SpeakerSignoreMoro"),
            "MAX" => Strings.Get("SpeakerMax"),
            _ => line.Speaker,
        };
        Body = line.Text;
    }
}
