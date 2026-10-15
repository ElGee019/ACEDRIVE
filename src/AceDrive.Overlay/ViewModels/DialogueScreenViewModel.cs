using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>Onboarding page 4: birthday gift-handover, one entry per page (Career Start item 4).</summary>
public sealed class DialogueScreenViewModel : ViewModelBase, IVoiceScreen, IDialogueScreen
{
    private readonly IScreenController _controller;
    private readonly VoiceBeat _beat;
    private readonly IReadOnlyList<DialogueLine> _lines;
    private int _index;

    public DialogueScreenViewModel(IScreenController controller, IReadOnlyList<DialogueLine> lines, VoiceBeat beat)
    {
        _controller = controller;
        _beat = beat;
        _lines = lines;
        ContinueCommand = new RelayCommand(Continue);
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("DialogueContinue")] = VoiceContinue,
        };
        Refresh();
    }

    public string SpeakerName { get => _speaker; private set => SetField(ref _speaker, value); }
    private string _speaker = "";

    public string Body { get => _body; private set => SetField(ref _body, value); }
    private string _body = "";

    public ICommand ContinueCommand { get; }

    /// <summary>Spoken label -&gt; action, for the coming voice phase (buttons stay mouse-first).</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; }

    /// <summary>True once the last page has been acknowledged (idle-hide resumes).</summary>
    public bool IsComplete { get; private set; }

    /// <summary>Teal-glow acknowledgment for a voice tap that finishes the dialogue.</summary>
    public bool ContinueFlash { get => _continueFlash; private set => SetField(ref _continueFlash, value); }
    private bool _continueFlash;

    private void Continue()
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

    // Page turns stay on this screen and act at once like a click; only the
    // finishing tap navigates, so only it flashes teal first and waits out
    // the shared beat.
    private void VoiceContinue()
    {
        if (_index + 1 < _lines.Count)
        {
            _index++;
            Refresh();
            return;
        }
        ContinueFlash = true;
        _beat.Defer(() =>
        {
            ContinueFlash = false;
            Finish();
        });
    }

    private void Finish()
    {
        IsComplete = true;
        _controller.FinishDialogue();
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
            _ => line.Speaker,
        };
        Body = line.Text;
    }
}
