using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Taxi job proposal page (PORTPLAN item 11, port of Unity
/// TaxiJobProposalScreen): the drawn passenger identity with its companion
/// suffix, the ride's two legs and the ACCEPT / DECLINE / BACK buttons plus
/// the optional job radius — the same information the Unity page shows.
/// Like Unity's generator, a proposal pairs the customer's home address with
/// a second address and a coin flip decides which leg is the pickup: the
/// home leg reads "home, {address}", the other "the {surname} residence,
/// {address}" after its drawn resident. Addresses stay placeholder names
/// until the Eifel table lands (PORTPLAN Step 1), so every proposal fits
/// every radius: Unity's RegenerateForRadius swap needs real coordinates and
/// arrives with the map, the field only commits its value meanwhile. Names
/// draw from the Germany cultural pools (PORTPLAN 61); DECLINE rolls a fresh
/// passenger like the Unity button does. Voice parity with the other pages:
/// every button answers to its text; the navigating taps glow first and wait
/// out the shared beat, DECLINE stays on the page and acts at once like a
/// click.
/// </summary>
public sealed class TaxiJobProposalViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    private readonly TaxiJobProposer _proposals;
    private readonly ILog _log;
    private readonly VoiceBeat _beat;
    private readonly ICommand _backCommand;
    private readonly ICommand _acceptCommand;
    private TaxiJobState _current = new();
    private int _radiusKm;

    public TaxiJobProposalViewModel(
        TaxiJobProposer proposals, ILog log, VoiceBeat beat,
        ICommand backCommand, ICommand acceptCommand)
    {
        _proposals = proposals;
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

    private void Generate()
    {
        TaxiProposal proposal = _proposals.Next();
        // Address A is the customer's home, Address B the second address;
        // the flip says which one becomes the pickup leg, exactly like
        // Unity steps the two resolved residents through isCustomerHomePickup.
        string home = Strings.Get("ProposalAddressA");
        string residence = Strings.Get("ProposalAddressB");
        string homeLeg = Strings.Format("ProposalLegHome", home);
        string residenceLeg = Strings.Format(
            "ProposalLegResidence", proposal.SecondResident.Surname, residence);
        _current = new TaxiJobState
        {
            Phase = JobPhase.Proposed,
            Customer = proposal.Customer,
            HomeIsPickup = proposal.HomeIsPickup,
            PickupName = proposal.HomeIsPickup ? home : residence,
            DropoffName = proposal.HomeIsPickup ? residence : home,
        };
        Phrase = Strings.Format(
            "ProposalPhrase",
            proposal.Customer.Full,
            proposal.HomeIsPickup ? homeLeg : residenceLeg,
            proposal.HomeIsPickup ? residenceLeg : homeLeg);
        _log.LogInfo($"job proposed: {proposal.Customer.Full}");
    }

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
        if (next != _radiusKm)
        {
            // No filtering yet: placeholder addresses carry no distance, so
            // the edit can only ever commit the limit for the map's arrival.
            _log.LogInfo(next == 0
                ? "proposal radius cleared: no limit"
                : $"proposal radius: {next} km");
            _radiusKm = next;
        }
        SetField(ref _radiusText, next == 0
            ? string.Empty
            : next.ToString(CultureInfo.CurrentCulture));
    }

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
