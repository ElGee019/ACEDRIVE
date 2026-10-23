using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The wallet page (PORTPLAN items 06/43/48/60): the driver's current CR,
/// XP total and bank saldo, the global XP leaderboard and the three
/// driver-rating skills, with one way back to the menu. All figures come
/// from the shared Core state and the standings service; the page owns only
/// presentation and a fresh read on open (Unity's Show semantics), so a job
/// settled since the last visit can never leave a stale number behind.
/// The leaderboard reuses the receipts' <see cref="StandingsBlock"/>, so the
/// offline placeholder and the live board event behave identically here.
/// Voice parity with the other pages: BACK navigates, so its voice tap glows
/// teal first and the page waits out the shared beat.
/// </summary>
public sealed class WalletScreenViewModel : ViewModelBase, IVoiceScreen, IJobsScreen
{
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public WalletScreenViewModel(
        DriverWallet wallet, IRivalsService rivals, ICommand backCommand, VoiceBeat beat)
    {
        _backCommand = backCommand;
        _beat = beat;
        CashLabel = Strings.Get("WalletCashLabel");
        CashAmount = Strings.Format("WalletCrAmount", wallet.Cash);
        XpLabel = Strings.Get("WalletXpLabel");
        XpAmount = Strings.Format("WalletXpAmount", wallet.Xp);
        BankLabel = Strings.Get("WalletBankLabel");
        BankAmount = Strings.Format("WalletCrAmount", wallet.BankBalance);
        ConcentrationLine = Strings.Format("WalletConcentration", wallet.Rating.Concentration);
        CorneringLine = Strings.Format("WalletCornering", wallet.Rating.Cornering);
        OffTheLineLine = Strings.Format("WalletOffTheLine", wallet.Rating.OffTheLine);
        Leaderboard = new StandingsBlock(rivals);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    public ICommand BackCommand { get; }

    /// <summary>The global XP leaderboard block (rank, board size and the
    /// movement since the last result).</summary>
    public StandingsBlock Leaderboard { get; }

    /// <summary>Left column of the account table: the three balance names.</summary>
    public string CashLabel { get; }

    public string XpLabel { get; }

    public string BankLabel { get; }

    /// <summary>Right column of the account table: the CR amount, two decimals.</summary>
    public string CashAmount { get; }

    /// <summary>Right column: the lifetime XP total (Unity receipts show two decimals).</summary>
    public string XpAmount { get; }

    /// <summary>Right column: the savings-bank saldo on the same ledger.</summary>
    public string BankAmount { get; }

    /// <summary>Unity driver-rating skill shares, one line each.</summary>
    public string ConcentrationLine { get; }

    public string CorneringLine { get; }

    public string OffTheLineLine { get; }

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out and the board subscription is
    // dropped (the receipt pages' Unhook rule).
    private void Back()
    {
        BackFlash = false;
        Leaderboard.Unhook();
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            // Back navigates: glow first, act at the end of the shared beat.
            [Strings.Get("WalletBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }
}
