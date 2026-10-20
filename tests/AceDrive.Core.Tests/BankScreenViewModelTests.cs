using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The bank screen runs headless against the wallet (item 48,
/// Unity BankScript): live saldo + preview lines, the withdraw flow, the
/// visit stamp and the two voice actions.</summary>
public sealed class BankScreenViewModelTests
{
    // Number parsing/formatting follows the culture; tests pin en-US.
    public BankScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    [Fact]
    public void Open_StampsTheVisit_AndRendersLiveLines()
    {
        // Unity Show: hasEnteredBank = true, then the saldo renders and the
        // input + preview open blank.
        var wallet = new DriverWallet(new DriverState(0, 0, Bank: 1000.0));
        var vm = new BankScreenViewModel(wallet, RelayCommandStub(), _ => { }, Beat());
        Assert.True(wallet.HasEnteredBank);
        Assert.Equal("Current Bank Saldo: CR1000.00", vm.SaldoLine);
        Assert.Equal(string.Empty, vm.AmountText);
        Assert.Equal(string.Empty, vm.AfterLine);
    }

    [Fact]
    public void Preview_ClampsToTheFloor()
    {
        var wallet = new DriverWallet(new DriverState(0, 0, Bank: 1234.5));
        var vm = new BankScreenViewModel(wallet, RelayCommandStub(), _ => { }, Beat());
        vm.AmountText = "100"; // inside the 234.50 max
        Assert.Equal("Balance After Withdrawal: CR1134.50", vm.AfterLine);
        vm.AmountText = "500"; // above the max — clamps, Unity Mathf.Clamp
        Assert.Equal("Balance After Withdrawal: CR1000.00", vm.AfterLine);
        vm.AmountText = "9999"; // clamps to the max too
        Assert.Equal("Balance After Withdrawal: CR1000.00", vm.AfterLine);
        vm.AmountText = "garbage"; // parse failure shows the untouched saldo
        Assert.Equal("Balance After Withdrawal: CR1234.50", vm.AfterLine);
    }

    [Fact]
    public void Withdraw_MovesBankToCash_AndPersistsThroughTheHost()
    {
        var wallet = new DriverWallet(new DriverState(0, 0, Bank: 1500.0));
        double? moved = null;
        var vm = new BankScreenViewModel(wallet, RelayCommandStub(), m => moved = m, Beat());
        vm.AmountText = "200";
        vm.WithdrawCommand.Execute(null);
        Assert.Equal(200.0, moved);
        Assert.Equal(1300.0, wallet.BankBalance);
        Assert.Equal(200.0, wallet.Cash);
        Assert.Equal("Current Bank Saldo: CR1300.00", vm.SaldoLine);
        // Unity clears the input after the move; the preview settles on the
        // fresh saldo.
        Assert.Equal(string.Empty, vm.AmountText);
        Assert.Equal("Balance After Withdrawal: CR1300.00", vm.AfterLine);
    }

    [Fact]
    public void Withdraw_ClampsAndNoOpsLikeUnity()
    {
        var wallet = new DriverWallet(new DriverState(0, 0, Bank: 1100.0));
        var moves = new List<double>();
        var vm = new BankScreenViewModel(wallet, RelayCommandStub(), moves.Add, Beat());
        vm.AmountText = "9999";
        vm.WithdrawCommand.Execute(null);
        // The oversized amount clamps to the max above the floor.
        Assert.Equal([100.0], moves);
        Assert.Equal(1000.0, wallet.BankBalance);

        moves.Clear();
        vm.AmountText = "50";
        vm.WithdrawCommand.Execute(null);
        Assert.Empty(moves); // floored account: the click does nothing
        Assert.Equal(1000.0, wallet.BankBalance);

        vm.AmountText = "not a number";
        vm.WithdrawCommand.Execute(null);
        Assert.Empty(moves); // failed parse: the click does nothing either
    }

    [Fact]
    public void BackCommand_RunsTheInjectedOne_AndClearsTheGlow()
    {
        bool fired = false;
        var vm = new BankScreenViewModel(
            new DriverWallet(), new RelayCommand(() => fired = true), _ => { }, Beat());
        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        vm.BackCommand.Execute(null); // a click during the beat: acts at once
        Assert.True(fired);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void Voice_EveryButtonAnswersToItsText()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool fired = false;
        var wallet = new DriverWallet(new DriverState(0, 0, Bank: 1500.0));
        var moves = new List<double>();
        var vm = new BankScreenViewModel(
            wallet, new RelayCommand(() => fired = true), moves.Add, beat);
        Assert.True(vm.VoiceActions.ContainsKey("WITHDRAW"));
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));

        // Withdraw stays on this page: the voice tap acts at once like a
        // click (the saldo is the acknowledgment) and never navigates.
        vm.AmountText = "200";
        vm.VoiceActions["WITHDRAW"].Invoke();
        Assert.Equal([200.0], moves);
        Assert.Equal(1300.0, wallet.BankBalance);
        Assert.False(fired);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void Voice_BackTap_GlowsFirst_NavigatesAtTheBeatEnd()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool fired = false;
        var vm = new BankScreenViewModel(
            new DriverWallet(), new RelayCommand(() => fired = true), _ => { }, beat);
        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(fired);
        DrainBeat(clock, beat);
        Assert.True(fired);
        Assert.False(vm.BackFlash);
    }

    private static VoiceBeat Beat() => new(new FakeClock());

    // What MainViewModel.Poll does at 10 Hz: wall time passes, the beat drains.
    private static void DrainBeat(FakeClock clock, VoiceBeat beat)
    {
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
    }

    private static RelayCommand RelayCommandStub() => new(() => { });
}
