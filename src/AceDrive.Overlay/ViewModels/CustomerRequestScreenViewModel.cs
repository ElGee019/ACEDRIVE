using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The customer request interstitial (PORTPLAN item 11, port of Unity
/// CustomerRequestScreen): a suffix-triggered passive ask, a speed challenge
/// or a three-option car-stat quiz, with a countdown shared by all kinds.
/// The page only reports the outcome; the host copies it onto the job, banks
/// the quiz history and opens the drop-off guidance when BACK closes the
/// page. The timer is ticked from <c>MainViewModel.Poll</c>, never a private
/// dispatcher timer (AGENTS.md: one loop).
/// </summary>
public sealed class CustomerRequestScreenViewModel :
    ViewModelBase, IVoiceScreen, IJobsScreen, IKeepPanelOpen
{
    private readonly CustomerRequest _request;
    private readonly VoiceBeat _beat;
    private readonly Action<CustomerRequestOutcome> _onFinished;
    private readonly int _timerSeconds;
    private readonly DateTime _deadline;
    private IReadOnlyList<CustomerRequestOption> _options = [];
    private CustomerRequestOutcome _outcome = CustomerRequestOutcome.None;
    private bool _finished;
    private bool _reported;

    public CustomerRequestScreenViewModel(
        CustomerRequest request, string customerName, int timerSeconds,
        IClock clock, VoiceBeat beat, Action<CustomerRequestOutcome> onFinished)
    {
        _request = request;
        _beat = beat;
        _onFinished = onFinished;
        _timerSeconds = Math.Max(0, timerSeconds);
        CustomerName = customerName;
        RemainingSeconds = _timerSeconds;
        _deadline = clock.UtcNow.AddSeconds(_timerSeconds);

        BackCommand = new RelayCommand(Back);
        BuildOptions();
        RebuildVoice();
    }

    /// <summary>The drawn passenger's full identity (the host's copy is the
    /// same one the proposal and receipt use).</summary>
    public string CustomerName { get; }

    public string RequestText => _request.Text;

    public int RemainingSeconds
    {
        get => _remainingSeconds;
        private set
        {
            if (SetField(ref _remainingSeconds, value))
            {
                OnPropertyChanged(nameof(TimeLine));
            }
        }
    }
    private int _remainingSeconds;

    /// <summary>Unity's "TIME REMAINING: {0}s" line.</summary>
    public string TimeLine => Strings.Format("RequestTime", RemainingSeconds);

    /// <summary>Shown until the request is answered or times out.</summary>
    public bool TimerVisible { get => _timerVisible; private set => SetField(ref _timerVisible, value); }
    private bool _timerVisible = true;

    /// <summary>The adaptive rows: three formatted quiz values, or ACCEPT /
    /// DECLINE. Empty once the request ended (Unity hides its buttons).</summary>
    public IReadOnlyList<CustomerRequestOption> Options
    {
        get => _options;
        private set
        {
            _options = value;
            OnPropertyChanged(nameof(Options));
        }
    }

    /// <summary>Outcome feedback, empty until the request ends.</summary>
    public string Feedback
    {
        get => _feedback;
        private set
        {
            if (SetField(ref _feedback, value))
            {
                OnPropertyChanged(nameof(HasFeedback));
            }
        }
    }
    private string _feedback = "";

    public bool HasFeedback => Feedback.Length > 0;

    /// <summary>Teal for a correct answer or an accepted ask, white for the
    /// wrong/declined/timeout lines (Unity's feedback colours).</summary>
    public bool FeedbackPositive
    {
        get => _feedbackPositive;
        private set => SetField(ref _feedbackPositive, value);
    }
    private bool _feedbackPositive;

    /// <summary>BACK appears only after answering or the timeout fires.</summary>
    public bool ShowBack { get => _showBack; private set => SetField(ref _showBack, value); }
    private bool _showBack;

    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// Advances the countdown on integer seconds. Called at 10 Hz by the
    /// host; a due timeout finishes the request exactly once and waits for
    /// BACK to report it, so the host never loses a timed-out quiz.
    /// </summary>
    public void Tick(DateTime now)
    {
        if (_finished)
        {
            return;
        }

        double left = (_deadline - now).TotalSeconds;
        int seconds = left <= 0.0
            ? 0
            : (int)Math.Ceiling(Math.Min(left, _timerSeconds));
        RemainingSeconds = seconds;
        if (seconds == 0)
        {
            Timeout();
        }
    }

    private void BuildOptions()
    {
        if (_request.Kind == CustomerRequestKind.CarQuiz && _request.Quiz is { } quiz)
        {
            List<CustomerRequestOption> options = [];
            for (int i = 0; i < quiz.Options.Count; i++)
            {
                int index = i;
                options.Add(new CustomerRequestOption(
                    Strings.Format(quiz.Category.UnitKey, quiz.Options[i]),
                    new RelayCommand(() => Answer(index))));
            }
            Options = options;
            return;
        }

        Options =
        [
            new CustomerRequestOption(Strings.Get("RequestAccept"), new RelayCommand(Accept)),
            new CustomerRequestOption(Strings.Get("RequestDecline"), new RelayCommand(Decline)),
        ];
    }

    private void Accept()
    {
        if (_finished)
        {
            return;
        }
        Finish(CustomerRequestGrader.Accept(_request), "RequestAccepted", positive: true);
    }

    private void Decline()
    {
        if (_finished)
        {
            return;
        }
        Finish(CustomerRequestGrader.Decline(_request), "RequestDeclined", positive: false);
    }

    private void Answer(int index)
    {
        if (_finished)
        {
            return;
        }

        CustomerRequestOutcome outcome = CustomerRequestGrader.Answer(_request, index);
        Finish(
            outcome,
            outcome.QuizAnsweredCorrectly ? "RequestCorrect" : "RequestWrong",
            outcome.QuizAnsweredCorrectly);
    }

    private void Timeout()
    {
        Finish(CustomerRequestGrader.Timeout(_request), "RequestTimeout", positive: false);
    }

    private void Finish(CustomerRequestOutcome outcome, string feedbackKey, bool positive)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _outcome = outcome;
        Feedback = Strings.Get(feedbackKey);
        FeedbackPositive = positive;
        TimerVisible = false;
        Options = [];
        ShowBack = true;
        RebuildVoice();
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out and the host hears the outcome
    // exactly once.
    private void Back()
    {
        BackFlash = false;
        if (!_finished || _reported)
        {
            return;
        }

        _reported = true;
        _onFinished(_outcome);
    }

    private void FlashThenBack()
    {
        BackFlash = true;
        _beat.Defer(Back);
    }

    private void RebuildVoice()
    {
        Dictionary<string, Action> actions = [];
        foreach (CustomerRequestOption option in Options)
        {
            actions[option.Text] = () => option.Command.Execute(null);
        }
        if (_finished)
        {
            actions[Strings.Get("DeliveryBack")] = FlashThenBack;
        }
        VoiceActions = actions;
    }
}
