using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Taxi job proposal page (PORTPLAN item 11, port of Unity
/// TaxiJobProposalScreen): the drawn passenger identity with its companion
/// suffix (dropped when the car seats only the customer), the ride's two legs
/// and the ACCEPT / DECLINE / BACK buttons plus the optional job radius — the
/// same information the Unity page shows.
/// Like Unity's generator, a proposal pairs the customer's home address with
/// a second address and a coin flip decides which leg is the pickup: the
/// home leg reads "home, {address}", the other "the {surname} residence,
/// {address}" after its resident. Addresses come from the address map's real
/// streets and houses, and the optional radius filters the draw like Unity's
/// TaxiJobGenerator (both legs within the committed km of home, 5 km floor).
/// Residents are reused per address through the persisted resident book, so
/// the same door keeps its homeowner (PORTPLAN 11/45/61); DECLINE rolls a
/// fresh proposal like the Unity button does. Voice parity with the other
/// pages: every button answers to its text; the navigating taps glow first
/// and wait out the shared beat, DECLINE stays on the page and acts at once
/// like a click.
/// </summary>
public sealed class TaxiJobProposalViewModel : ViewModelBase, IVoiceScreen
{
    private readonly TaxiJobProposer _proposals;
    private readonly AddressMap _map;
    private readonly Func<AddressLocation?> _home;
    private readonly Func<int> _passengerCapacity;
    private readonly ILog _log;
    private readonly VoiceBeat _beat;
    private readonly ICommand _backCommand;
    private readonly ICommand _acceptCommand;
    private TaxiJobState _current = new();
    private int _radiusKm;

    public TaxiJobProposalViewModel(
        TaxiJobProposer proposals, AddressMap map, Func<AddressLocation?> home,
        Func<int> passengerCapacity, ILog log, VoiceBeat beat,
        ICommand backCommand, ICommand acceptCommand)
    {
        _proposals = proposals;
        _map = map;
        _home = home;
        _passengerCapacity = passengerCapacity;
        _log = log;
        _beat = beat;
        _backCommand = backCommand;
        _acceptCommand = acceptCommand;

        AcceptCommand = new RelayCommand(Accept);
        DeclineCommand = new RelayCommand(Decline);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    /// <summary>The Unity page's whole job text in one resx template, so
    /// translators own the copy and placeholders survive.</summary>
    public string Phrase { get => _phrase; private set => SetField(ref _phrase, value); }
    private string _phrase = "";

    /// <summary>Radius field text. Committing (TextBox default: on focus
    /// loss, Unity's onEndEdit parity) rewrites it to the committed value,
    /// empty when unlimited.</summary>
    public string RadiusText
    {
        get => _radiusText;
        set => CommitRadius(value);
    }
    private string _radiusText = "";

    /// <summary>The committed radius (0 = no limit), Unity's taxiJobRadiusKm
    /// semantics. In-memory until the persistent save lands (PORTPLAN 51).</summary>
    public int RadiusKm => _radiusKm;

    /// <summary>The dirty-car refusal line (PORTPLAN item 33, Unity
    /// TaxiJobLifecycleHandler's "No way I am getting into a car this dirty"):
    /// the host sets it when a filth-25 pickup is refused, and using it as the
    /// copy is resx. Cleared on every fresh proposal.</summary>
    public string RefusalText
    {
        get => _refusalText;
        private set
        {
            if (SetField(ref _refusalText, value))
            {
                OnPropertyChanged(nameof(HasRefusal));
            }
        }
    }
    private string _refusalText = "";

    /// <summary>Whether the refusal line shows (a message is on screen).</summary>
    public bool HasRefusal => RefusalText.Length > 0;

    /// <summary>Which leg is the customer's home on the shown proposal
    /// (Unity's isCustomerHomePickup): true = home is the pickup.</summary>
    public bool HomeIsPickup => _current.HomeIsPickup;

    public ICommand AcceptCommand { get; }
    public ICommand DeclineCommand { get; }
    public ICommand BackCommand { get; }

    /// <summary>Teal-glow acknowledgment for the navigating voice taps.</summary>
    public bool AcceptFlash { get => _acceptFlash; private set => SetField(ref _acceptFlash, value); }
    private bool _acceptFlash;

    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>Every taxi tap opens on a fresh proposal, like the Unity
    /// screen showing a freshly preloaded job on each visit: two residents
    /// drawn and the home/residence coin flip.</summary>
    public void Open() => Generate();

    /// <summary>Shows the host's dirty-car refusal on the page (the job is
    /// never accepted). The host owns the friendship penalty; the page only
    /// paints the copy it is handed.</summary>
    public void ShowRefusal(string message) => RefusalText = message;

    private void Generate()
    {
        RefusalText = "";
        TaxiProposal proposal = _proposals.Next(
            _passengerCapacity(), _home(), _radiusKm);
        // Both legs are described before the flip (Unity resolves the two
        // residents first); the flip then decides which door becomes the
        // pickup, exactly like isCustomerHomePickup.
        string home = Describe(proposal.CustomerHome, "ProposalAddressA");
        string residence = Describe(proposal.SecondResidence, "ProposalAddressB");
        string homeLeg = Strings.Format("ProposalLegHome", home);
        string residenceLeg = Strings.Format(
            "ProposalLegResidence", proposal.SecondResident.Surname, residence);
        _current = new TaxiJobState
        {
            Phase = JobPhase.Proposed,
            Customer = proposal.Customer,
            HomeIsPickup = proposal.HomeIsPickup,
            CustomerHome = proposal.CustomerHome,
            PickupAddress = proposal.HomeIsPickup
                ? proposal.CustomerHome
                : proposal.SecondResidence,
            DropoffAddress = proposal.HomeIsPickup
                ? proposal.SecondResidence
                : proposal.CustomerHome,
            PickupName = proposal.HomeIsPickup ? home : residence,
            DropoffName = proposal.HomeIsPickup ? residence : home,
        };
        Phrase = Strings.Format(
            "ProposalPhrase",
            proposal.Customer.Full,
            proposal.HomeIsPickup ? homeLeg : residenceLeg,
            proposal.HomeIsPickup ? residenceLeg : homeLeg);
        _log.LogInfo(
            $"job proposed: {proposal.Customer.Full} " +
            $"{_current.PickupName} -> {_current.DropoffName}");
    }

    // The street shape is shared with the HUD; the proposal adds the map's
    // city, Unity's town in FormatAddress. A missing address (the map cannot
    // supply two streets) keeps the placeholder leg so the page stays usable.
    private string Describe(AddressLocation? address, string fallbackKey) =>
        address is { } a
            ? Strings.Format("ProposalAddress", AddressFormatter.Format(a), _map.City)
            : Strings.Get(fallbackKey);

    private void Accept()
    {
        ClearFlash();
        _acceptCommand.Execute(_current);
    }

    private void Decline()
    {
        _log.LogInfo("proposal declined");
        Generate();
    }

    private void Back()
    {
        ClearFlash();
        _backCommand.Execute(null);
    }

    private void CommitRadius(string? raw)
    {
        int next = TaxiJobRadius.Normalize(raw, _radiusKm);
        bool changed = next != _radiusKm;
        if (changed)
        {
            _radiusKm = next;
            _log.LogInfo(next == 0
                ? "proposal radius cleared: no limit"
                : $"proposal radius: {next} km");
        }

        SetField(ref _radiusText, next == 0
            ? string.Empty
            : next.ToString(CultureInfo.CurrentCulture));

        // Unity swaps in a proposal that fits the new limit
        // (RegenerateForRadius). Resident resolution is synchronous here, so
        // the page needs no "looking" state.
        if (changed && !CurrentFitsRadius())
        {
            Generate();
        }
    }

    private bool CurrentFitsRadius() =>
        AddressBook.JobFitsRadius(
            _map, _home(), _current.PickupAddress, _current.DropoffAddress, _radiusKm);

    // Accept and back navigate, so a voice tap flashes teal first and the
    // page waits out the shared beat (delivery precedent); a click (or the
    // beat's own run) clears the glow, so a revisit never shows stale teal.
    private void FlashThen(string target, Action act)
    {
        AcceptFlash = target == "accept";
        BackFlash = target == "back";
        _beat.Defer(act);
    }

    private void ClearFlash()
    {
        AcceptFlash = false;
        BackFlash = false;
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("ProposalAccept")] = () => FlashThen("accept", Accept),
            [Strings.Get("ProposalDecline")] = Decline,
            [Strings.Get("DeliveryBack")] = () => FlashThen("back", Back),
        };
    }
}
