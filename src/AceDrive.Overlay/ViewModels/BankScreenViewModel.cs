using System.Globalization;
using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The savings-bank screen (PORTPLAN 48, Unity BankScript): a withdraw-only
/// page over the wallet. Unity parity — no deposit, the amount clamps to
/// the 1000 saldo floor instead of failing (the preview shows the clamped
/// resulting balance), a parse failure shows the untouched saldo, and a
/// floored account answers the WITHDRAW tap with nothing. Opening stamps
/// hasEnteredBank (Unity's Show); a withdraw persists through the caller's
/// shared save — the screen never touches files itself (SRP).
/// Voice parity with the other pages: WITHDRAW answers to its text and acts
/// at once like a click (it stays on this page), BACK navigates, so its
/// voice tap glows teal first and the page waits out the shared beat.
/// </summary>
public sealed class BankScreenViewModel : ViewModelBase, IVoiceScreen
{
    private readonly DriverWallet _wallet;
    private readonly ICommand _backCommand;
    private readonly Action<double> _onWithdraw;
    private readonly VoiceBeat _beat;

    public BankScreenViewModel(
        DriverWallet wallet, ICommand backCommand, Action<double> onWithdraw, VoiceBeat beat)
    {
        _wallet = wallet;
        _backCommand = backCommand;
        _onWithdraw = onWithdraw;
        _beat = beat;
        // Unity Show: the visit stamps once, then RefeshCurrentBalance +
        // ResetInput — the saldo opens live, the input and the preview
        // open blank.
        wallet.MarkBankEntered();
        ReloadSaldo();
        WithdrawCommand = new RelayCommand(Withdraw);
        BackCommand = new RelayCommand(Back);
        RebuildVoice();
    }

    public ICommand BackCommand { get; }

    public ICommand WithdrawCommand { get; }

    public string SaldoLine { get => _saldoLine; private set => SetField(ref _saldoLine, value); }
    private string _saldoLine = "";

    public string AmountText
    {
        get => _amountText;
        set
        {
            _amountText = value;
            // Unity OnInputChanged: every change (typed, cleared, cleared by
            // a withdraw) repaints the preview; a parse failure shows the
            // untouched saldo.
            ReloadPreview();
        }
    }
    private string _amountText = "";

    public string AfterLine { get => _afterLine; private set => SetField(ref _afterLine, value); }
    private string _afterLine = "";

    /// <summary>Teal-glow acknowledgment for the navigating BACK tap.</summary>
    public bool BackFlash { get => _backFlash; private set => SetField(ref _backFlash, value); }
    private bool _backFlash;

    public IReadOnlyDictionary<string, Action> VoiceActions { get; private set; } =
        new Dictionary<string, Action>();

    // The one path back: a click and the voice tap's deferred end both land
    // here, so the glow clears on the way out — a click during a pending
    // voice tap never leaves a stale teal behind (delivery-page precedent).
    private void Back()
    {
        BackFlash = false;
        _backCommand.Execute(null);
    }

    private void RebuildVoice()
    {
        VoiceActions = new Dictionary<string, Action>
        {
            // Withdraw stays on this page: it acts at once like a click.
            [Strings.Get("BankWithdraw")] = Withdraw,
            // Back navigates: glow first, act at the end of the shared beat.
            [Strings.Get("BankBack")] = () =>
            {
                BackFlash = true;
                _beat.Defer(Back);
            },
        };
    }

    // Unity OnWithdrawClicked: a failed parse, a non-positive amount or a
    // floored account (max withdraw <= 0) leave the screen untouched — the
    // click legally does nothing. Otherwise the clamped amount moves bank →
    // cash and the caller persists it (Unity SaveManager.Save).
    private void Withdraw()
    {
        if (!double.TryParse(_amountText, NumberStyles.Float, CultureInfo.CurrentCulture,
                out double amount))
        {
            return;
        }
        if (amount <= 0.0) return;
        double moved = _wallet.Withdraw(amount);
        if (moved <= 0.0) return;
        ReloadSaldo();
        // Unity clears the input after the move, which fires the input
        // handler and leaves the preview on the fresh saldo.
        AmountText = "";
        _onWithdraw(moved);
    }

    // Unity RefreshCurrentBalance: "Current Bank Saldo: CR{0:F2}".
    private void ReloadSaldo() => SaldoLine = Strings.Format("BankSaldo", _wallet.BankBalance);

    // Unity OnInputChanged: the preview subtracts the input clamped into
    // 0..maxWithdraw; below the floor the clamp is 0, so the preview shows
    // the saldo itself.
    private void ReloadPreview()
    {
        double max = BankRules.MaxWithdraw(_wallet.BankBalance);
        AfterLine = double.TryParse(_amountText, NumberStyles.Float, CultureInfo.CurrentCulture,
            out double amount)
            ? Strings.Format("BankAfter", _wallet.BankBalance - Math.Clamp(amount, 0.0, max))
            : Strings.Format("BankAfter", _wallet.BankBalance);
    }
}
