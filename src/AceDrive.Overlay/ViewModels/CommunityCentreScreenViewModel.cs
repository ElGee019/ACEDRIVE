using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The community centre's hiring page (PORTPLAN item 54, port of Unity
/// CommunityCentre's hire mode): after the introduction is banked, entering
/// the centre opens this interior instead of the licence conversation. The
/// HIRE AN EMPLOYEE button reveals the week's five residents, each row shows
/// the familiarity the hire would warm, and HIRE moves that resident onto the
/// crew through <see cref="EmployeeRoster"/>.
///
/// The page writes nothing itself: the roster banks the hire and the host's
/// callback logs it and warms Signore Moro's familiarity. Starting with the
/// rows hidden mirrors Unity, where the panel opens on the button and the
/// candidates only appear on the tap; a hire hides them again so the page
/// always shows one state at a time.
/// </summary>
public sealed class CommunityCentreScreenViewModel
    : ViewModelBase, IVoiceScreen, IPoiScreen
{
    private readonly EmployeeRoster _roster;
    private readonly Func<IReadOnlyList<string>> _residentNames;
    private readonly Func<int> _week;
    private readonly IFriendshipStore _friendships;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;
    private readonly Action<string> _onHired;

    public CommunityCentreScreenViewModel(
        EmployeeRoster roster,
        Func<IReadOnlyList<string>> residentNames,
        Func<int> week,
        IFriendshipStore friendships,
        ICommand backCommand,
        VoiceBeat beat,
        Action<string> onHired)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(residentNames);
        ArgumentNullException.ThrowIfNull(week);
        ArgumentNullException.ThrowIfNull(friendships);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(onHired);

        _roster = roster;
        _residentNames = residentNames;
        _week = week;
        _friendships = friendships;
        _backCommand = backCommand;
        _beat = beat;
        _onHired = onHired;
        ShowCandidatesCommand = new RelayCommand(ShowCandidates);
        HireCommand = new ParameterizedCommand<HireCandidateRow>(Hire);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>The HIRE AN EMPLOYEE button: reveals (or refreshes) the roster.</summary>
    public ICommand ShowCandidatesCommand { get; }

    /// <summary>One candidate row's HIRE button.</summary>
    public ICommand HireCommand { get; }

    /// <summary>Leaves the centre through the host's BACK command.</summary>
    public ICommand BackCommand { get; }

    /// <summary>The page's one body line: what the centre is for.</summary>
    public string Greeting { get; } = Strings.Get("CommunityCentreGreeting");

    /// <summary>The week's candidates, empty until the button is tapped.</summary>
    public IReadOnlyList<HireCandidateRow> Candidates
    {
        get => _candidates;
        private set => SetField(ref _candidates, value);
    }
    private IReadOnlyList<HireCandidateRow> _candidates = [];

    /// <summary>True while the candidate rows are on screen.</summary>
    public bool HasCandidates
    {
        get => _hasCandidates;
        private set => SetField(ref _hasCandidates, value);
    }
    private bool _hasCandidates;

    /// <summary>True when the tap found nobody to offer.</summary>
    public bool ShowEmpty
    {
        get => _showEmpty;
        private set => SetField(ref _showEmpty, value);
    }
    private bool _showEmpty;

    /// <summary>The confirmation after a hire, empty the rest of the time.</summary>
    public string HiredLine
    {
        get => _hiredLine;
        private set
        {
            if (SetField(ref _hiredLine, value))
            {
                OnPropertyChanged(nameof(ShowHired));
            }
        }
    }
    private string _hiredLine = "";

    /// <summary>True while the hire confirmation is on screen.</summary>
    public bool ShowHired => HiredLine.Length > 0;

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // Unity's OnHireEmployeeClicked: refresh the week's roster, then paint its
    // rows. The existing rows are replaced, so a tap never stacks duplicates.
    private void ShowCandidates()
    {
        CancelPendingVoice();
        _roster.RefreshCandidates(_residentNames(), _week());
        BuildRows();
        ShowEmpty = !HasCandidates;
        HiredLine = "";
        RebuildVoice();
    }

    // Unity's CreateHireRow: the owner name and the familiarity the hire
    // warms. The roster is the source of the names; the friendship store is
    // the source of the level, so the row can never disagree with the receipt.
    private void BuildRows()
    {
        var rows = new List<HireCandidateRow>(_roster.Candidates.Count);
        foreach (string name in _roster.Candidates)
        {
            rows.Add(new HireCandidateRow(
                name,
                Strings.Format(
                    "CommunityCentreFriendship", _friendships.GetLevel(name))));
        }

        Candidates = rows;
        HasCandidates = rows.Count > 0;
    }

    // Unity's OnHireClicked: the candidate joins the crew, the rows hide and
    // one confirmation replaces them. The roster banks the hire before the
    // page reports it; a refused hire (a stale row) leaves the page untouched.
    private void Hire(HireCandidateRow? row)
    {
        if (row is null)
        {
            return;
        }

        // The hire is newer input than a pending BACK: drop its deferred verb
        // so the page cannot close behind the confirmation.
        CancelPendingVoice();
        if (!_roster.Hire(row.Name))
        {
            return;
        }

        _onHired(row.Name);
        Candidates = [];
        HasCandidates = false;
        ShowEmpty = false;
        HiredLine = Strings.Format("CommunityCentreHired", row.Name);
        RebuildVoice();
    }

    // A click (or a newer voice verb) is newer input than a pending BACK tap:
    // drop its deferred navigation and the stale glow, like the addresses page.
    private void CancelPendingVoice()
    {
        _beat.Cancel();
        BackFlash = false;
    }

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out (bank-page precedent).
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>
        {
            // HIRE AN EMPLOYEE stays on this page: it acts at once like a click.
            [Strings.Get("CommunityCentreHire")] = ShowCandidates,
        };
        if (HasCandidates)
        {
            // A candidate's spoken name hires exactly like their row's button.
            foreach (HireCandidateRow row in Candidates)
            {
                HireCandidateRow candidate = row;
                actions[candidate.Name] = () => Hire(candidate);
            }
        }

        // Back navigates: glow first, act at the end of the shared beat.
        actions[Strings.Get("CommunityCentreBack")] = () =>
        {
            BackFlash = true;
            _beat.Defer(Back);
        };
        VoiceActions = actions;
    }
}
