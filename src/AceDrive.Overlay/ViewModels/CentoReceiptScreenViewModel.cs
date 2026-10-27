using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Cento Miglia receipt page (PORTPLAN item 49, port of Unity
/// CentoMigliaScreen's CentoReceipt panel): congratulations, the finishing
/// place, the placement purse, the club reputation the race earned, the
/// assertivity/garage/class calculation line (minus the assertivity stat the
/// port has not brought over), the Star of the Show outcome, the award or
/// certificate that joined the car, the wallet and CONTINUE.
///
/// Unity also prints the final standings; the port has no NPC field yet, so
/// the receipt hides that block until the field lands. All values are computed
/// once by the settle and never change here.
/// </summary>
public sealed class CentoReceiptScreenViewModel : ViewModelBase, IVoiceScreen, IKeepPanelOpen
{
    private readonly CentoReceipt _receipt;
    private readonly DriverWallet _wallet;
    private readonly ICommand _continueCommand;
    private readonly VoiceBeat _beat;

    /// <summary>Teal-glow acknowledgment for the navigating CONTINUE tap.</summary>
    public bool ContinueFlash
    {
        get => _continueFlash;
        private set => SetField(ref _continueFlash, value);
    }
    private bool _continueFlash;

    public CentoReceiptScreenViewModel(
        CentoReceipt receipt, DriverWallet wallet, VoiceBeat beat,
        ICommand continueCommand)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(wallet);
        ArgumentNullException.ThrowIfNull(beat);
        ArgumentNullException.ThrowIfNull(continueCommand);
        _receipt = receipt;
        _wallet = wallet;
        _beat = beat;
        _continueCommand = continueCommand;
        ContinueCommand = new RelayCommand(Continue);
        RebuildVoice();
    }

    /// <summary>"Congratulations you completed the week 3 Cento Miglia for the …".</summary>
    public string CongratsLine => Strings.Format(
        "CentoReceiptCongrats", _receipt.Week, _receipt.ClubLabel);

    /// <summary>"1st PLACE" - Unity's place line with its ordinal suffix.</summary>
    public string PlaceLine => _receipt.Placement switch
    {
        1 => Strings.Get("CentoPlaceFirst"),
        2 => Strings.Get("CentoPlaceSecond"),
        3 => Strings.Get("CentoPlaceThird"),
        _ => Strings.Format("CentoPlaceOther", _receipt.Placement),
    };

    /// <summary>"+ 200.00 CR (Placement Earnings, Tier 1, Rep x1.00032)".</summary>
    public string EarningsLine => Strings.Format(
        "CentoReceiptEarnings",
        _receipt.PlacementEarnings, _receipt.Tier, _receipt.EndReputation);

    /// <summary>"Participating with the … earned you 0.00032 Club Reputation."</summary>
    public string ReputationLine => Strings.Format(
        "CentoReceiptReputation", _receipt.CarName, _receipt.EarnedReputation);

    /// <summary>The garage/class calculation (Unity's assertivity is not ported).</summary>
    public string CalculationLine => Strings.Format(
        "CentoReceiptCalculation", _receipt.OwnedCars, _receipt.CarClass);

    /// <summary>True when the award was won: the reward line turns teal.</summary>
    public bool StarWon => _receipt.StarWon;

    public string RewardLine => _receipt.StarWon
        ? Strings.Format("CentoReceiptStarWon", _receipt.StarPrize)
        : Strings.Get("CentoReceiptStarLost");

    /// <summary>Where the award/certificate went: the car's carried list, which
    /// the trunk page shows under its glove compartment because the catalogue
    /// defines no bite for it.</summary>
    public string ItemLine => _receipt.StarWon
        ? Strings.Get("CentoReceiptAwardStored")
        : Strings.Get("CentoReceiptCertificateStored");

    /// <summary>"+ 1500.00 CR" - the purse the race banked.</summary>
    public string TotalLine => Strings.Format("CentoReceiptTotal", _receipt.TotalEarned);

    /// <summary>The wallet after the settle, like every other receipt page.</summary>
    public string WalletLine => Strings.Format("CentoReceiptWallet", _wallet.Cash);

    public ICommand ContinueCommand { get; }

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
