using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The customer request interstitial (PORTPLAN item 11, port of Unity
/// CustomerRequestScreen): a suffix-triggered passive ask, a forced speed
/// challenge or a three-option car-stat quiz. Passive and quiz asks run on
/// the 14 s answer countdown; the speed challenge has no answer, so its page
/// shows the host's delivery deadline instead and never times out. The page
/// only reports the outcome; the host copies it onto the job, banks the quiz
/// history and opens the drop-off guidance when BACK closes the page. The
/// timer is ticked from <c>MainViewModel.Poll</c>, never a private dispatcher
/// timer (AGENTS.md: one loop).
/// </summary>
public sealed class CustomerRequestScreenViewModel :
    ViewModelBase, IVoiceScreen, IJobsScreen, IKeepPanelOpen
{
    private readonly CustomerRequest _request;
    private readonly VoiceBeat _beat;
    private readonly Action<CustomerRequestOutcome> _onFinished;
    private readonly int _timerSeconds;
    private readonly DateTime _deadline;
    private readonly bool _speedChallenge;
    private readonly DateTime? _speedDeadline;
    private IReadOnlyList<CustomerRequestOption> _options = [];
    private CustomerRequestOutcome _outcome = CustomerRequestOutcome.None;
    private bool _finished;
    private bool _reported;

    public CustomerRequestScreenViewModel(
        CustomerRequest request, string customerName, int timerSeconds,
        IClock clock, VoiceBeat beat, Action<CustomerRequestOutcome> onFinished,
        DateTime? speedDeadline = null)
    {
        _request = request;
        _beat = beat;
        _onFinished = onFinished;
        _timerSeconds = Math.Max(0, timerSeconds);
        _speedChallenge = request.Kind == CustomerRequestKind.SpeedChallenge;
        _speedDeadline = speedDeadline;
        CustomerName = customerName;
        _deadline = clock.UtcNow.AddSeconds(_timerSeconds);
        if (_speedChallenge)
        {
            TimerVisible = _speedDeadline is not null;
            RemainingSeconds = RemainingAt(clock.UtcNow);
            SpeedChallengeLate = _speedDeadline is { } deadline
                && deadline < clock.UtcNow;
        }
        else
        {
            RemainingSeconds = _timerSeconds;
        }

        BackCommand = new RelayCommand(Back);
        BuildOptions();
        RebuildVoice();
    }

    /// <summary>True for the forced speed challenge: no ACCEPT/DECLINE, and
    /// the page's clock is the delivery deadline instead of the answer timer.</summary>
    public bool IsSpeedChallenge => _speedChallenge;

    /// <summary>True once the delivery deadline has passed, so the page can
    /// paint the countdown line in its warning colour.</summary>
    public bool SpeedChallengeLate
    {
        get => _speedChallengeLate;
        private set
        {
            if (SetField(ref _speedChallengeLate, value))
            {
                OnPropertyChanged(nameof(TimeLine));
            }
        }
    }
    private bool _speedChallengeLate;

    /// <summary>The drawn passenger's full identity (the host's copy is the
    /// same one the proposal and receipt use).</summary>
    public string CustomerName { get; }

    public string RequestText => _request.Text;

    /// <summary>The ask hides once the request ends: the feedback line
    /// replaces the question instead of sitting underneath it. That covers the
    /// suffix-triggered passive ask as well, whose "I'll tip you when we
    /// arrive" answer would otherwise leave the request line above it. Only
    /// the forced speed challenge keeps its line, and it never ends here.</summary>
    public bool RequestTextVisible
    {
        get => _requestTextVisible;
        private set => SetField(ref _requestTextVisible, value);
    }
    private bool _requestTextVisible = true;

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

    /// <summary>Unity's "TIME REMAINING: {0}s" line for an answer, or the
    /// speed challenge's delivery clock once that request is up.</summary>
    public string TimeLine
    {
        get
        {
            if (!_speedChallenge)
            {
                return Strings.Format("RequestTime", RemainingSeconds);
            }

            if (_speedDeadline is null)
            {
                return string.Empty;
            }

            return SpeedChallengeLate
                ? Strings.Get("SpeedChallengeLate")
                : Strings.Format("SpeedChallengeTime", ClockText(RemainingSeconds));
        }
    }

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
    /// host; a due timeout finishes an answer request exactly once and waits
    /// for BACK to report it, so the host never loses a timed-out quiz. The
    /// speed challenge only repaints its delivery clock.
    /// </summary>
    public void Tick(DateTime now)
    {
        if (_speedChallenge)
        {
            // The speed challenge is already accepted; its clock is the
            // delivery deadline, not the answer countdown, so it never times
            // the page out or changes the outcome.
            if (_speedDeadline is not null)
            {
                RemainingSeconds = RemainingAt(now);
                SpeedChallengeLate = _speedDeadline < now;
            }
            return;
        }

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

    private int RemainingAt(DateTime now)
    {
        if (_speedDeadline is not { } deadline || deadline < now)
        {
            return 0;
        }

        double seconds = Math.Ceiling((deadline - now).TotalSeconds);
        return seconds >= int.MaxValue ? int.MaxValue : (int)seconds;
    }

    private static string ClockText(int seconds) =>
        $"{seconds / 60}:{seconds % 60:00}";

    private void BuildOptions()
    {
        if (_speedChallenge)
        {
            // Forced challenge: the customer does not ask, so there is nothing
            // to accept or decline. BACK only returns to the driving guidance.
            ShowBack = true;
            Options = [];
            return;
        }

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
        RequestTextVisible = false;
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
        if (_reported)
        {
            return;
        }

        if (_speedChallenge)
        {
            // The forced challenge was accepted at the pickup; BACK only
            // dismisses its page. Report the same outcome the old ACCEPT did.
            _reported = true;
            _onFinished(CustomerRequestGrader.Accept(_request));
            return;
        }

        if (!_finished)
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
        if (ShowBack)
        {
            actions[Strings.Get("DeliveryBack")] = FlashThenBack;
        }
        VoiceActions = actions;
    }
}
