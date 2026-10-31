using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The car-haul page (PORTPLAN item 20): the offer's facts plus ACCEPT while the
/// job waits, or the active leg's task and target after it was accepted. One
/// page for both states because the facts are the same car, dealer and
/// customer; only the action and the task line change. Unity splits them across
/// the job-selection button and the map banner, which the overlay does not own.
/// </summary>
public sealed class CarHaulScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly CarHaulOffer _offer;
    private readonly CarHaulPhase _phase;
    private readonly string _city;
    private readonly ICommand _acceptCommand;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public CarHaulScreenViewModel(
        CarHaulOffer offer,
        CarHaulPhase phase,
        string city,
        ICommand acceptCommand,
        ICommand backCommand,
        VoiceBeat beat,
        bool isOffer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(city);
        ArgumentNullException.ThrowIfNull(acceptCommand);
        ArgumentNullException.ThrowIfNull(backCommand);
        ArgumentNullException.ThrowIfNull(beat);

        _offer = offer;
        _phase = phase;
        _city = city;
        _acceptCommand = acceptCommand;
        _backCommand = backCommand;
        _beat = beat;
        IsOffer = isOffer;
        AcceptCommand = new RelayCommand(Accept);

        RebuildVoice();
    }

    /// <summary>True while the offer waits for ACCEPT; false on the active leg.</summary>
    public bool IsOffer { get; }

    /// <summary>The active-leg block (task line) is hidden on the offer page.</summary>
    public bool IsJob => !IsOffer;

    public string CarLine => Strings.Format(
        "HaulOfferCar", $"{_offer.Car.Brand} {_offer.Car.Model}");

    public string DealerLine => Strings.Format(
        "HaulOfferDealer",
        _offer.Dealer.Name,
        _offer.Dealer.Address.StreetName,
        _offer.Dealer.Address.HouseNumber,
        _city);

    public string CustomerLine => Strings.Format(
        "HaulOfferCustomer",
        _offer.Customer.BaseName,
        _offer.CustomerAddress.StreetName,
        _offer.CustomerAddress.HouseNumber,
        _city);

    public string TierLine => Strings.Format("HaulOfferTier", _offer.PayoutTier);

    /// <summary>
    /// The leg's own instruction: pick the car up at the dealer while the car
    /// still waits there, then deliver it to the customer. Unity's map banner
    /// says the same two sentences.
    /// </summary>
    public string TaskLine => _phase == CarHaulPhase.Offered
        ? Strings.Format("HaulJobPickUpTask", CarLineValue, _offer.Dealer.Name)
        : Strings.Format("HaulJobDeliverTask", _offer.Customer.BaseName);

    public ICommand AcceptCommand { get; }

    public bool AcceptFlash
    {
        get => _acceptFlash;
        private set => SetField(ref _acceptFlash, value);
    }
    private bool _acceptFlash;

    public bool BackFlash
    {
        get => _backFlash;
        private set => SetField(ref _backFlash, value);
    }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private string CarLineValue => $"{_offer.Car.Brand} {_offer.Car.Model}";

    private void Accept()
    {
        AcceptFlash = false;
        _acceptCommand.Execute(null);
    }

    private void AcceptByVoice()
    {
        AcceptFlash = true;
        _beat.Defer(Accept);
    }

    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void BackByVoice()
    {
        BackFlash = true;
        _beat.Defer(Back);
    }

    private void RebuildVoice()
    {
        var actions = new Dictionary<string, Action>
        {
            [Strings.Get("DeliveryBack")] = BackByVoice,
        };
        if (IsOffer)
        {
            actions[Strings.Get("HaulAccept")] = AcceptByVoice;
        }

        VoiceActions = actions;
    }
}
