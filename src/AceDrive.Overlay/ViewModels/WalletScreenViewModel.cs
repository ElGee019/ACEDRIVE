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
/// presentation and a fresh read on open (Unity's Show semantics) — and again
/// when an interruption hands it back (<see cref="IRefreshableScreen"/>) — so
/// neither a job settled since the last visit nor the crash repair that came
/// over it can leave a stale number behind.
/// The leaderboard reuses the receipts' <see cref="StandingsBlock"/>, so the
/// offline placeholder and the live board event behave identically here.
/// Voice parity with the other pages: BACK navigates, so its voice tap glows
/// teal first and the page waits out the shared beat.
/// </summary>
public sealed class WalletScreenViewModel : ViewModelBase, IVoiceScreen, IRefreshableScreen
{
    private readonly DriverWallet _wallet;
    private readonly ICommand _backCommand;
    private readonly VoiceBeat _beat;

    public WalletScreenViewModel(
        DriverWallet wallet, IRivalsService rivals, ICommand backCommand, VoiceBeat beat)
    {
        _wallet = wallet;
        _backCommand = backCommand;
        _beat = beat;
        CashLabel = Strings.Get("WalletCashLabel");
        XpLabel = Strings.Get("WalletXpLabel");
        BankLabel = Strings.Get("WalletBankLabel");
        Leaderboard = new StandingsBlock(rivals);
        BackCommand = new RelayCommand(Back);
        Refresh();
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
    public string CashAmount
    {
        get => _cashAmount;
        private set => SetField(ref _cashAmount, value);
    }
    private string _cashAmount = "";

    /// <summary>Right column: the lifetime XP total (Unity receipts show two decimals).</summary>
    public string XpAmount
    {
        get => _xpAmount;
        private set => SetField(ref _xpAmount, value);
    }
    private string _xpAmount = "";

    /// <summary>Right column: the savings-bank saldo on the same ledger.</summary>
    public string BankAmount
    {
        get => _bankAmount;
        private set => SetField(ref _bankAmount, value);
    }
    private string _bankAmount = "";

    /// <summary>Unity driver-rating skill shares, one line each.</summary>
    public string ConcentrationLine
    {
        get => _concentrationLine;
        private set => SetField(ref _concentrationLine, value);
    }
    private string _concentrationLine = "";

    public string CorneringLine
    {
        get => _corneringLine;
        private set => SetField(ref _corneringLine, value);
    }
    private string _corneringLine = "";

    public string OffTheLineLine
    {
        get => _offTheLineLine;
        private set => SetField(ref _offTheLineLine, value);
    }
    private string _offTheLineLine = "";

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    /// <summary>
    /// Repaints the account table from the live wallet: the panel hands this
    /// page back after the crash repair's broker page, and the bill it took
    /// must show on the CR line.
    /// </summary>
    public void Refresh()
    {
        CashAmount = Strings.Format("WalletCrAmount", _wallet.Cash);
        XpAmount = Strings.Format("WalletXpAmount", _wallet.Xp);
        BankAmount = Strings.Format("WalletCrAmount", _wallet.BankBalance);
        ConcentrationLine = Strings.Format("WalletConcentration", _wallet.Rating.Concentration);
        CorneringLine = Strings.Format("WalletCornering", _wallet.Rating.Cornering);
        OffTheLineLine = Strings.Format("WalletOffTheLine", _wallet.Rating.OffTheLine);
    }

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
