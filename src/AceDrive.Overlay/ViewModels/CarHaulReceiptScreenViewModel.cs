using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Car-haul receipt page (PORTPLAN item 20, port of Unity
/// CarHaulingScreen): the settled delivery with the hauled car, the drive
/// duration, the earned amount, the driver rating, the class tier behind
/// the per-minute rate, and the dealer-owner familiarity. No XP or
/// standings block — Unity settles the haul's cash alone, so the page
/// follows. The familiarity box stays hidden until the dealer POIs land
/// (Unity hides it too when the dealer has no owner). Money lines are
/// computed once and never change. CONTINUE navigates back, so a voice tap
/// flashes teal first and waits out the shared beat (receipt-page
/// precedent).
/// </summary>
public sealed class CarHaulReceiptScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly CarHaulReceipt _receipt;
    private readonly DriverWallet _wallet;
    private readonly VoiceBeat _beat;
    private readonly ICommand _continueCommand;

    public CarHaulReceiptScreenViewModel(
        CarHaulReceipt receipt, DriverWallet wallet,
        VoiceBeat beat, ICommand continueCommand)
    {
        _receipt = receipt;
        _wallet = wallet;
        _beat = beat;
        _continueCommand = continueCommand;

        ContinueCommand = new RelayCommand(Continue);
        RebuildVoice();
    }

    public string CarLine => Strings.Format("HaulReceiptCar", _receipt.CarName);

    public string TimeLine => ReceiptText.DriveDuration(
        _receipt.DriveHours, _receipt.DriveMinutesRemainder);

    public ReceiptLine EarnedLine => new(
        Strings.Get("HaulReceiptEarned"), ReceiptText.Amount(_receipt.TotalEarned));

    public string DriverRatingLine => Strings.Format(
        "HaulReceiptDriverRating", _receipt.DriverRating);

    public string TierLine => Strings.Format("HaulReceiptTier", _receipt.Tier);

    public string RateLine => Strings.Format("HaulReceiptRate", _receipt.RatePerMin);

    public string WalletLine => Strings.Format("ReceiptWallet", _wallet.Cash);

    /// <summary>Unity familiarity sentence (dealer-owner bands), empty while
    /// the job has no dealer POI behind it.</summary>
    public bool HasFriendship => _receipt.HasFriendship;

    public string FriendshipLine => HasFriendship
        ? ReceiptText.FriendshipSentence(
            _receipt.DealerOwnerName, _receipt.DealerFriendshipLevel)
        : string.Empty;

    /// <summary>Teal highlight when the dealer owner crossed a tier.</summary>
    public bool FriendshipHighlight => _receipt.DealerFriendshipTierIncreased;

    public ICommand ContinueCommand { get; }
    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash { get => _continueFlash; private set => SetField(ref _continueFlash, value); }
    private bool _continueFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    private void Continue()
    {
        ContinueFlash = false;
        _continueCommand.Execute(null);
    }

    private void FlashThenContinue()
    {
        ContinueFlash = true;
        _beat.Defer(Continue);
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("ReceiptContinue")] = FlashThenContinue,
        };
    }
}
