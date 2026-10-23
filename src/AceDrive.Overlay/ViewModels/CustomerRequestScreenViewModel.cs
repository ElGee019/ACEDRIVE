using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The customer request interstitial (PORTPLAN item 11, port of Unity
/// CustomerRequestScreen): a suffix-triggered passive ask or a three-option
/// car-stat quiz, both on the table's answer countdown (19 s in shipped
/// data). A forced speed challenge is already agreed, so its page is a short
/// read-in that shows the delivery clock and closes itself: no answer
/// countdown and no button. The page only reports the outcome; the host
/// copies it onto the job, banks the quiz history and restores the drop-off
/// guidance. The timer is ticked from <c>MainViewModel.Poll</c>, never a
/// private dispatcher timer (AGENTS.md: one loop).
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
    private readonly DateTime? _speedDismissAt;
    private IReadOnlyList<CustomerRequestOption> _options = [];
    private CustomerRequestOutcome _outcome = CustomerRequestOutcome.None;
    private bool _finished;
    private bool _reported;

    public CustomerRequestScreenViewModel(
        CustomerRequest request, string customerName, int timerSeconds,
        IClock clock, VoiceBeat beat, Action<CustomerRequestOutcome> onFinished,
        DateTime? speedDeadline = null, TimeSpan? speedReadIn = null)
    {
        _request = request;
        _beat = beat;
        _onFinished = onFinished;
        _timerSeconds = Math.Max(0, timerSeconds);
        _speedChallenge = request.Kind == CustomerRequestKind.SpeedChallenge;
        _speedDeadline = speedDeadline;
        CustomerName = customerName;
        _deadline = clock.UtcNow.AddSeconds(_timerSeconds);
        RemainingSeconds = _timerSeconds;
        if (_speedChallenge)
        {
            // A forced challenge asks nothing: no answer clock and no button,
            // just the delivery deadline and a read-in that closes the page.
            TimerVisible = false;
            _speedDismissAt = speedReadIn is { } readIn
                ? clock.UtcNow + readIn
                : null;
            RefreshDeliveryClock(clock.UtcNow);
        }

        BackCommand = new RelayCommand(Back);
        BuildOptions();
        RebuildVoice();
    }

    /// <summary>True for the forced speed challenge: no answer countdown, no
    /// options and no BACK; the page dismisses itself after its read-in while
    /// the delivery clock keeps running.</summary>
    public bool IsSpeedChallenge => _speedChallenge;

    /// <summary>True once the delivery deadline has passed, so the page can
    /// paint the deadline line in its warning colour.</summary>
    public bool SpeedChallengeLate
    {
        get => _speedChallengeLate;
        private set
        {
            if (SetField(ref _speedChallengeLate, value))
            {
                OnPropertyChanged(nameof(DeliveryLine));
            }
        }
    }
    private bool _speedChallengeLate;

    /// <summary>True when the forced challenge's delivery deadline is drawn
    /// on its read-in page (never for a passive ask or a quiz).</summary>
    public bool DeliveryVisible => _speedChallenge && _speedDeadline is not null;

    /// <summary>The drawn passenger's full identity (the host's copy is the
    /// same one the proposal and receipt use).</summary>
    public string CustomerName { get; }

    public string RequestText => _request.Text;

    /// <summary>The ask hides once the request ends: the feedback line
    /// replaces the question instead of sitting underneath it. That covers the
    /// suffix-triggered passive ask as well, whose "I'll tip you when we
    /// arrive" answer would otherwise leave the request line above it. The
    /// forced speed challenge keeps its line; its page never enters feedback.</summary>
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

    /// <summary>The passive ask's and quiz's answer countdown ("TIME
    /// REMAINING: {0}s"), Unity's quiz line; hidden on the forced challenge.</summary>
    public string TimeLine => Strings.Format("RequestTime", RemainingSeconds);

    /// <summary>The forced challenge's delivery clock (mm:ss or TOO LATE);
    /// empty for the passive ask and the quiz.</summary>
    public string DeliveryLine =>
        !DeliveryVisible
            ? string.Empty
            : SpeedChallengeLate
                ? Strings.Get("SpeedChallengeLate")
                : Strings.Format("SpeedChallengeTime", ClockText(_deliverySeconds));

    private int _deliverySeconds;

    /// <summary>Shown until the passive ask is answered/timed out; the forced
    /// challenge draws its delivery clock instead.</summary>
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

    /// <summary>BACK appears only after answering or the timeout fires (never
    /// on the forced challenge, which has no button).</summary>
    public bool ShowBack { get => _showBack; private set => SetField(ref _showBack, value); }
    private bool _showBack;

    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// Advances the clocks on integer seconds. Called at 10 Hz by the host: a
    /// passive ask or quiz that runs out finishes exactly once and waits for
    /// BACK to report it (so the host never loses a timed-out ask). The forced
    /// challenge has nothing to answer: it repaints its delivery clock and
    /// reports the agreed outcome when its read-in ends, which restores the
    /// task text with the same clock still running.
    /// </summary>
    public void Tick(DateTime now)
    {
        if (_finished)
        {
            return;
        }

        if (_speedChallenge)
        {
            RefreshDeliveryClock(now);
            if (_speedDismissAt is { } dismissAt && now >= dismissAt)
            {
                FinishSpeedChallenge();
            }
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

    // The speed challenge's delivery deadline: its own clock, repainted on
    // whole seconds, never part of the answer window.
    private void RefreshDeliveryClock(DateTime now)
    {
        if (_speedDeadline is null)
        {
            return;
        }

        _deliverySeconds = RemainingAt(now);
        SpeedChallengeLate = _speedDeadline < now;
        OnPropertyChanged(nameof(DeliveryLine));
    }

    // The forced challenge's read-in is over: report the agreed outcome once.
    // The host restores the drop-off guidance, carrying the same deadline the
    // page was showing, so the countdown continues without a second page.
    private void FinishSpeedChallenge()
    {
        if (_reported)
        {
            return;
        }

        _finished = true;
        _reported = true;
        _onFinished(CustomerRequestGrader.Accept(_request));
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
            // Forced challenge: the customer does not ask, so there is
            // nothing to accept or decline and no button to close a page
            // that dismisses itself after its read-in.
            ShowBack = false;
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

    // The one path back for an answered ask: a click and the voice tap's
    // deferred end both land here, so the glow clears on the way out and the
    // host hears the outcome exactly once. The forced challenge has no BACK:
    // its own read-in reports the outcome.
    private void Back()
    {
        BackFlash = false;
        if (_reported || !_finished)
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
