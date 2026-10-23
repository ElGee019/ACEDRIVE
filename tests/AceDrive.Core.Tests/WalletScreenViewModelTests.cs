using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The wallet page runs headless against the wallet + standings
/// service: the account figures, the global XP leaderboard, the three
/// driver-rating skills and the one voice/click path back.</summary>
public sealed class WalletScreenViewModelTests
{
    // Number formatting follows the culture; tests pin en-US.
    public WalletScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");

    [Fact]
    public void Open_RendersTheLiveAccountSummary()
    {
        var wallet = new DriverWallet(new DriverState(
            123.45, 678.0, Bank: 1500.0,
            Cornering: 31.0, Concentration: 42.0, OffTheLine: 53.5));

        var vm = NewViewModel(wallet);

        Assert.Equal("CURRENT CR", vm.CashLabel);
        Assert.Equal("CR123.45", vm.CashAmount);
        Assert.Equal("CURRENT XP", vm.XpLabel);
        Assert.Equal("XP678.00", vm.XpAmount);
        Assert.Equal("CURRENT BANK SALDO", vm.BankLabel);
        Assert.Equal("CR1500.00", vm.BankAmount);
        Assert.Equal("Concentration: 42%", vm.ConcentrationLine);
        Assert.Equal("Cornering: 31%", vm.CorneringLine);
        Assert.Equal("Off-The-Line: 54%", vm.OffTheLineLine);
    }

    [Fact]
    public void Open_UsesTheNewGameRatingForAPreRatingSave()
    {
        // A save written before PORTPLAN 06 carries no skills; the driver
        // opens on Unity's new-game overall of 20 instead of three zeros.
        var vm = NewViewModel(new DriverWallet(new DriverState(0.0, 0.0)));

        Assert.Equal("Concentration: 20%", vm.ConcentrationLine);
        Assert.Equal("Cornering: 20%", vm.CorneringLine);
        Assert.Equal("Off-The-Line: 20%", vm.OffTheLineLine);
    }

    [Fact]
    public void Leaderboard_ShowsTheUnknownPlaceholderThenTheLiveBoard()
    {
        var rivals = new FakeRivals();
        var vm = NewViewModel(new DriverWallet(), rivals);

        Assert.Equal("Your XP world standings is —/—.", vm.Leaderboard.Line);

        rivals.Emit(new RivalsResult(7, 41, 3, 10, false));

        Assert.Equal("Your XP world standings is 7th of 41.", vm.Leaderboard.Line);
        Assert.Equal("You gained 3 positions.", vm.Leaderboard.DeltaLine);
        Assert.True(vm.Leaderboard.Changed);
    }

    [Fact]
    public void BackCommand_RunsTheInjectedOne_AndStopsFollowingTheBoard()
    {
        var rivals = new FakeRivals();
        rivals.Result = new RivalsResult(7, 41, 0, 7, false);
        bool fired = false;
        var vm = NewViewModel(new DriverWallet(), rivals, () => fired = true);

        vm.BackCommand.Execute(null);
        rivals.Emit(new RivalsResult(1, 41, 6, 7, false));

        Assert.True(fired);
        // The page unhooked on the way out, so the leave-time line stays.
        Assert.Equal("Your XP world standings is 7th of 41.", vm.Leaderboard.Line);
    }

    [Fact]
    public void Voice_BackTapGlowsFirst_ThenNavigatesAndClearsTheGlow()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool fired = false;
        var vm = new WalletScreenViewModel(
            new DriverWallet(), new FakeRivals(),
            new RelayCommand(() => fired = true), beat);

        Assert.True(vm.VoiceActions.ContainsKey("BACK"));
        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(fired);

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();

        Assert.True(fired);
        Assert.False(vm.BackFlash);
    }

    private static WalletScreenViewModel NewViewModel(
        DriverWallet wallet, FakeRivals? rivals = null, Action? back = null) =>
        new(wallet, rivals ?? new FakeRivals(),
            new RelayCommand(back ?? (() => { })), new VoiceBeat(new FakeClock()));
}
