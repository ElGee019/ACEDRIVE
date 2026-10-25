using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The Cento Miglia page (PORTPLAN item 49, port of Unity CentoMigliaScreen's
/// tier list and detail panel). Sunday's calendar row opens it inside the
/// 8am-8pm gate; the page prints the week's club reputation, the eight tiers
/// with the level each asks for, and — once a tier is picked — its description
/// and the driver's eligibility lines.
///
/// The page only formats and gates: the tier copy rides in from
/// Data/cento_miglia.json, the gates come from <see cref="CentoMiglia"/>, and
/// the facts arrive as one record. Entering a tier would start the race, which
/// the overlay does not simulate yet, so ENTER closes the page exactly like
/// Unity's screen does — leaving the outcome to the race port.
///
/// Unity's weekly one-entry gate and the registration number are absent with
/// it: both are written by a finished race. The header keeps Unity's reputation
/// floor (a raw 0 prints as 1.00000) and the detail panel keeps its three
/// lines; the assertivity line stays out for the same reason it does on the
/// customer-request page (the stat has no port yet).
/// </summary>
public sealed class CentoMigliaScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly CentoMigliaFacts _facts;
    // The page's one way out: BACK on the list and ENTER on a tier both close
    // it. Unity pops back to the jobs page, which is the calendar here.
    private readonly ICommand _close;
    private CentoTierRow? _selected;

    public CentoMigliaScreenViewModel(
        IReadOnlyList<CentoTier> tiers, CentoMigliaFacts facts, ICommand close)
    {
        _facts = facts;
        _close = close;
        Tiers = BuildRows(tiers, facts);
        WeekLine = Strings.Format("CentoWeek", facts.Week, facts.Year);
        ReputationLine = Strings.Format(
            "CentoReputation", CentoMiglia.DisplayReputation(facts.ClubReputation));
        TierCommand = new ParameterizedCommand<CentoTierRow>(OpenTier);
        BackCommand = new RelayCommand(Back);
        EnterCommand = new RelayCommand(Enter);
        RebuildVoice();
    }

    /// <summary>The club rows in the data file's order (Unity's button order).</summary>
    public IReadOnlyList<CentoTierRow> Tiers { get; }

    /// <summary>"Week {n}, {year}" - Unity's TextDate.</summary>
    public string WeekLine { get; }

    /// <summary>"YOUR CLUB REPUTATION: 1.00000" - Unity's TextClubReputation.</summary>
    public string ReputationLine { get; }

    /// <summary>The tier whose detail panel is open; null shows the list.</summary>
    public CentoTierRow? Selected
    {
        get => _selected;
        private set
        {
            if (!SetField(ref _selected, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsTierList));
            OnPropertyChanged(nameof(IsDetail));
            OnPropertyChanged(nameof(DetailLabel));
            OnPropertyChanged(nameof(DetailDescription));
            OnPropertyChanged(nameof(EligibilityText));
            RebuildVoice();
        }
    }

    /// <summary>True on the tier list (Unity's CentoA panel).</summary>
    public bool IsTierList => _selected is null;

    /// <summary>True on a tier's detail (Unity's CentoB panel).</summary>
    public bool IsDetail => _selected is not null;

    /// <summary>The open tier's label, the detail panel's heading.</summary>
    public string DetailLabel => _selected?.Label ?? string.Empty;

    /// <summary>The open tier's description (content, never resx).</summary>
    public string DetailDescription => _selected?.Tier.Description ?? string.Empty;

    /// <summary>
    /// The detail panel's eligibility block, one line each like Unity's
    /// TextEligibility: the reputation the tier asks for, the garage size and
    /// the current car's class. Unity's fourth line (assertivity) has no stat
    /// behind it in this port.
    /// </summary>
    public string EligibilityText
    {
        get
        {
            if (_selected is not { } row)
            {
                return string.Empty;
            }

            string reputation = row.Tier.Id == CentoMiglia.GentlemenDrivers
                ? Strings.Get("CentoFreeEntry")
                : Strings.Format(
                    "CentoEligibilityReputation",
                    _facts.ClubReputation, row.Tier.ReputationRequired);
            string cars = Strings.Format(
                _facts.CarsOwned == 1 ? "CentoOwnsCar" : "CentoOwnsCars",
                _facts.CarsOwned);
            return string.Join(
                '\n', reputation, cars, Strings.Format("CentoCarClass", _facts.CarClass));
        }
    }

    /// <summary>Opens a tier's detail; a locked row never gets here (the button
    /// is disabled), so the guard only answers a stale command.</summary>
    public ICommand TierCommand { get; }

    /// <summary>BACK: the detail returns to the list, the list leaves the page.</summary>
    public ICommand BackCommand { get; }

    /// <summary>ENTER EVENT: closes the page, waiting on the race port.</summary>
    public ICommand EnterCommand { get; }

    /// <summary>
    /// Spoken text -&gt; action. The list answers to each unlocked tier's label
    /// and BACK; the detail answers to ENTER EVENT and BACK. Locked tiers stay
    /// out, like their disabled buttons.
    /// </summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private static CentoTierRow[] BuildRows(
        IReadOnlyList<CentoTier> tiers, CentoMigliaFacts facts)
    {
        int decade = CentoMiglia.EraDecade(facts.Year);
        var rows = new CentoTierRow[tiers.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            CentoTier tier = tiers[i];
            rows[i] = new CentoTierRow(
                tier,
                string.Format(CultureInfo.CurrentCulture, tier.Label, decade),
                Strings.Format("CentoTierRequirement", tier.ReputationRequired),
                CentoMiglia.IsTierEligible(
                    tier, facts.ClubReputation, facts.HasDiscoveredStreets));
        }
        return rows;
    }

    private void OpenTier(CentoTierRow? row)
    {
        if (row is null || !row.Eligible)
        {
            return;
        }

        Selected = row;
    }

    private void Back()
    {
        if (IsDetail)
        {
            Selected = null;
            return;
        }

        _close.Execute(null);
    }

    private void Enter() => _close.Execute(null);

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>
        {
            [Strings.Get("DeliveryBack")] = Back,
        };
        if (_selected is not null)
        {
            actions[Strings.Get("CentoEnter")] = Enter;
        }
        else
        {
            foreach (CentoTierRow row in Tiers)
            {
                if (row.Eligible)
                {
                    actions[row.Label] = () => OpenTier(row);
                }
            }
        }
        VoiceActions = actions;
    }
}
