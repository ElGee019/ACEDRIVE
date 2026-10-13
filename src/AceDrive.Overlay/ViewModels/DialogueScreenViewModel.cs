using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>Onboarding page 4: birthday gift-handover, one entry per page (Career Start item 4).</summary>
public sealed class DialogueScreenViewModel : ViewModelBase, IVoiceScreen, IDialogueScreen
{
    private readonly IScreenController _controller;
    private readonly IReadOnlyList<DialogueLine> _lines;
    private int _index;

    public DialogueScreenViewModel(IScreenController controller, IReadOnlyList<DialogueLine> lines)
    {
        _controller = controller;
        _lines = lines;
        ContinueCommand = new RelayCommand(Continue);
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("DialogueContinue")] = Continue,
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

    private void Continue()
    {
        if (_index + 1 < _lines.Count)
        {
            _index++;
            Refresh();
        }
        else
        {
            IsComplete = true;
            _controller.FinishDialogue();
        }
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
