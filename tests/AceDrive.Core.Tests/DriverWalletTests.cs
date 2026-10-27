using AceDrive.Core;

namespace AceDrive.Core.Tests;

public sealed class DriverWalletTests
{
    private static TaxiReceipt Receipt(double total = 13.78, double xp = 0.082) =>
        new(8.2, 1, [], 43.15, 10.0, 53.15, 10.63, 1, 50.0, total, xp, 1, 1, 100, "", 0, false);

    [Fact]
    public void Fresh_StartsWithTheStartingCash()
    {
        // A brand-new game must afford the first jobs' overhead, so the
        // wallet opens on the seed rather than on nothing.
        var wallet = new DriverWallet();
        Assert.Equal(100.0, wallet.Cash);
        Assert.Equal(DriverWallet.StartingCash, wallet.Cash);
        Assert.Equal(0.0, wallet.Xp);
    }

    [Fact]
    public void Resume_ClampsNegativeSaves()
    {
        var wallet = new DriverWallet(new DriverState(-5.0, -1.0));
        Assert.Equal(0.0, wallet.Cash);
        Assert.Equal(0.0, wallet.Xp);
    }

    [Fact]
    public void AddReceipt_AccumulatesCashAndXp()
    {
        // The empty fixture keeps the expectation to the receipt delta alone
        // (the fresh-start seed is covered above).
        var wallet = new DriverWallet(new DriverState(0.0, 0.0));
        wallet.AddReceipt(Receipt());
        wallet.AddReceipt(Receipt());
        Assert.Equal(27.56, wallet.Cash);
        Assert.Equal(0.164, wallet.Xp, precision: 9);
    }

    [Fact]
    public void TrySpend_MovesCashOrRefuses()
    {
        // Unity's buy gate (CarDealerScreen.OnBuyClicked): a short wallet
        // returns without buying, so the caller keeps its action a no-op.
        var wallet = new DriverWallet(new DriverState(100.0, 0.0));
        Assert.False(wallet.TrySpend(120.0));
        Assert.Equal(100.0, wallet.Cash);
        Assert.True(wallet.TrySpend(wallet.Cash));
        Assert.Equal(0.0, wallet.Cash);
        // Nothing to spend, nothing moves: a free car is not a purchase path.
        Assert.False(wallet.TrySpend(0.0));
        Assert.False(wallet.TrySpend(-5.0));
        Assert.Equal(0.0, wallet.Cash);
    }

    [Fact]
    public void TrySpend_RoundsTheLedgerToTwoDecimals()
    {
        // 100 - 40.005 = 59.995: half away from zero, the ledger's rule.
        var wallet = new DriverWallet(new DriverState(100.0, 0.0));
        Assert.True(wallet.TrySpend(40.005));
        Assert.Equal(60.00, wallet.Cash);
    }

    // The delivery twin: the run's profit IS the wallet delta (Unity
    // pays out received + charge wholesale-prepaid).
    private static DeliveryReceipt Delivery(double earned = 143.40, double xp = 0.082) =>
        new(8.2, [], 129.40, 49.00, 63.00, earned, xp, 10, 1, 100, "", 0, false, 0, 0.0);

    [Fact]
    public void AddDelivery_BanksTheRunProfit()
    {
        var wallet = new DriverWallet(new DriverState(0.0, 0.0));
        wallet.AddDelivery(Delivery());
        Assert.Equal(143.40, wallet.Cash);
        Assert.Equal(0.082, wallet.Xp, precision: 9);
        // A money-losing cart dips the cash instead of crashing it.
        wallet.AddDelivery(Delivery(earned: -1.40));
        Assert.Equal(142.00, wallet.Cash);
    }

    // The haul twin: Unity settles cash and nothing else — no XP for
    // handing over a car (the driver rating scales the pay instead).
    private static CarHaulReceipt Haul(double earned = 74.70) =>
        new("Test Hatch", 6, 50, 2.49, earned, 30);

    [Fact]
    public void AddHaul_BanksCashOnly()
    {
        var wallet = new DriverWallet(new DriverState(10.0, 5.0));
        wallet.AddHaul(Haul());
        Assert.Equal(84.70, wallet.Cash);
        Assert.Equal(5.0, wallet.Xp);
    }

    [Fact]
    public void AddBonus_LandsInCashOnly()
    {
        // The login-bonus route (item 47): CR only — XP stays untouched,
        // exactly like Unity's GrantDailyBonusIfDue (cash += bonus).
        var wallet = new DriverWallet(new DriverState(99.05, 3.0));
        wallet.AddBonus(4);
        Assert.Equal(103.05, wallet.Cash);
        Assert.Equal(3.0, wallet.Xp);
    }

    [Fact]
    public void Fresh_WasNeverInABankButStartsAtTheUnitySeed()
    {
        // Unity PlayerDefaults bankAmount = 1000; the record default carries
        // it, so a driver.json from before the bank migrates implicitly.
        var wallet = new DriverWallet();
        Assert.Equal(1000.0, wallet.BankBalance);
        Assert.False(wallet.HasEnteredBank);

        // A legal balance is never 0, so any legal resume loads as-is.
        var saved = new DriverWallet(new DriverState(10.0, 5.0, Bank: 2450.25));
        Assert.Equal(2450.25, saved.BankBalance);
        Assert.True(new DriverWallet(new DriverState(0, 0, HasEnteredBank: true)).HasEnteredBank);
    }

    [Fact]
    public void Resume_ClampsANegativeBank()
    {
        var wallet = new DriverWallet(new DriverState(0, 0, Bank: -5.0));
        Assert.Equal(0.0, wallet.BankBalance);
    }

    [Fact]
    public void Withdraw_MovesBankAboveTheFloorIntoCash()
    {
        var wallet = new DriverWallet(new DriverState(50.0, 0.0, Bank: 1500.0));
        Assert.Equal(412.0, wallet.Withdraw(412.0));
        Assert.Equal(1088.0, wallet.BankBalance); // above the 1000 floor
        Assert.Equal(462.0, wallet.Cash);
    }

    [Fact]
    public void Withdraw_ClampsOversizedAmounts()
    {
        var wallet = new DriverWallet(new DriverState(0.0, 0.0, Bank: 1234.5));
        Assert.Equal(234.5, wallet.Withdraw(9999.0));
        Assert.Equal(1000.0, wallet.BankBalance);
        Assert.Equal(234.5, wallet.Cash);
    }

    [Fact]
    public void Withdraw_NoOpsOnAFlooredAccount()
    {
        // Unity OnWithdrawClicked: maxWithdraw <= 0 → return. Nothing moves
        // even when the tap carries an amount.
        var wallet = new DriverWallet(new DriverState(0.0, 0.0, Bank: 1000.0));
        Assert.Equal(0.0, wallet.Withdraw(50.0));
        Assert.Equal(1000.0, wallet.BankBalance);
        Assert.Equal(0.0, wallet.Withdraw(-3.0)); // non-positive never moves
        Assert.Equal(0.0, wallet.Cash);
    }

    [Fact]
    public void ApplyInterest_CompoundsOnTheBank()
    {
        // Unity ApplyForCompletion credits into bankAmount before the save,
        // so the next settle reads the grown balance (compounding).
        var wallet = new DriverWallet(new DriverState(0, 0, Bank: 1000.0));
        wallet.ApplyInterest(0.1);
        wallet.ApplyInterest(0.3);
        Assert.Equal(1000.4, wallet.BankBalance);
        Assert.Equal(0.0, wallet.Cash); // the payout stays out of cash
    }

    [Fact]
    public void Rating_DefaultsToNewGame_ResumesAndRidesTheSnapshot()
    {
        // A pre-rating save carries the new-game profile through the record
        // defaults; a resumed save keeps its own three shares.
        Assert.Equal(DriverRating.NewGame, new DriverWallet().Rating);

        var resumed = new DriverWallet(new DriverState(
            0, 0, Cornering: 30.0, Concentration: 40.0, OffTheLine: 50.0));
        Assert.Equal(new DriverRating(30.0, 40.0, 50.0), resumed.Rating);
        Assert.Equal(40.0, resumed.Rating.Overall, precision: 9);

        DriverState snapshot = resumed.Snapshot();
        Assert.Equal(30.0, snapshot.Cornering);
        Assert.Equal(40.0, snapshot.Concentration);
        Assert.Equal(50.0, snapshot.OffTheLine);
    }

    [Fact]
    public void Rating_ClampsAHandEditedZero()
    {
        var wallet = new DriverWallet(new DriverState(0, 0, Cornering: 0.0));
        Assert.Equal(11.01, wallet.Rating.Cornering);
    }

    [Fact]
    public void RatingSkillEvents_RideTheSnapshot()
    {
        var wallet = new DriverWallet(new DriverState(0, 0));

        wallet.AddCornering(1.0);
        wallet.AddConcentration(-1.0);
        wallet.AddOffTheLine(2.0);

        DriverState snapshot = wallet.Snapshot();
        Assert.Equal(21.0, snapshot.Cornering);
        Assert.Equal(19.0, snapshot.Concentration);
        Assert.Equal(22.0, snapshot.OffTheLine);
    }

    [Fact]
    public void Snapshot_RoundTrips()
    {
        var wallet = new DriverWallet(new DriverState(100.0, 5.0));
        wallet.AddReceipt(Receipt());
        DriverState snapshot = wallet.Snapshot();
        Assert.Equal(113.78, snapshot.Cash);
        Assert.Equal(5.082, snapshot.Xp, precision: 9);
        // The bank slice rides the same snapshot so one save covers the
        // whole wallet (Unity saves bankAmount with the player data).
        Assert.Equal(1000.0, snapshot.Bank);
        Assert.False(snapshot.HasEnteredBank);
        wallet.MarkBankEntered();
        wallet.ApplyInterest(0.1);
        snapshot = wallet.Snapshot();
        Assert.Equal(1000.1, snapshot.Bank);
        Assert.True(snapshot.HasEnteredBank);
    }

    [Fact]
    public void ChargeUnavoidable_TakesWhatTheCashCovers()
    {
        // A premium or a tow fee is a commitment, not a purchase: it takes up
        // to the cash and never leaves the ledger negative.
        var wallet = new DriverWallet(new DriverState(10.0, 0.0));
        Assert.Equal(6.90, wallet.ChargeUnavoidable(6.90));
        Assert.Equal(3.10, wallet.Cash, precision: 6);

        Assert.Equal(3.10, wallet.ChargeUnavoidable(99.0)); // clamps to the cash
        Assert.Equal(0.0, wallet.Cash);
        Assert.Equal(0.0, wallet.ChargeUnavoidable(5.0));   // nothing left to take
        Assert.Equal(0.0, wallet.ChargeUnavoidable(0.0));   // a free charge is a no-op
    }

    [Fact]
    public void InsurancePayout_GoesToTheEscrow_ThenToCash()
    {
        var wallet = new DriverWallet(new DriverState(50.0, 0.0));
        wallet.AddInsurancePayout(4.5);
        wallet.AddInsurancePayout(0.0); // ignored
        Assert.Equal(4.5, wallet.PendingInsurance);
        Assert.Equal(50.0, wallet.Cash); // the escrow is not cash yet

        Assert.Equal(4.5, wallet.CollectInsurancePayout());
        Assert.Equal(0.0, wallet.PendingInsurance);
        Assert.Equal(54.5, wallet.Cash);
        Assert.Equal(0.0, wallet.CollectInsurancePayout()); // empty escrow no-ops
    }

    [Fact]
    public void InsuranceFields_RideTheSnapshot()
    {
        var wallet = new DriverWallet(new DriverState(0.0, 0.0));
        wallet.AddInsurancePayout(2.0);
        wallet.MarkInsuranceCharged("2026-10-12");

        DriverState snapshot = wallet.Snapshot();
        Assert.Equal(2.0, snapshot.PendingInsurance);
        Assert.Equal("2026-10-12", snapshot.LastInsuranceChargeDate);

        var resumed = new DriverWallet(snapshot);
        Assert.Equal(2.0, resumed.PendingInsurance);
        Assert.Equal("2026-10-12", resumed.LastInsuranceChargeDate);
    }

    [Fact]
    public void CreditEarned_CountsEarningsOnly()
    {
        var wallet = new DriverWallet(new DriverState(0.0, 0.0));
        wallet.AddReceipt(Receipt());     // 13.78
        wallet.AddDelivery(Delivery());   // 143.40
        wallet.AddHaul(Haul());           // 74.70
        wallet.AddBonus(4);               // +4.00
        wallet.AddInsurancePayout(9.0);   // escrow first: not earnings yet
        _ = wallet.CollectInsurancePayout();

        Assert.Equal(244.88, wallet.CreditEarned, precision: 6);

        // Moving money around never counts: the ToDo tip measures work, so
        // Unity's bank-withdrawal quirk is deliberately not ported.
        wallet.ApplyInterest(5.0);
        _ = wallet.Withdraw(10.0);
        wallet.ChargeUnavoidable(3.0);
        Assert.Equal(244.88, wallet.CreditEarned, precision: 6);
    }

    [Fact]
    public void CreditEarned_ResumesAndNeverGoesNegative()
    {
        Assert.Equal(
            12.5,
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: 12.5)).CreditEarned);

        // A hand-edited negative reads as a fresh counter.
        Assert.Equal(
            0.0,
            new DriverWallet(new DriverState(0.0, 0.0, CreditEarned: -3.0)).CreditEarned);

        var wallet = new DriverWallet(new DriverState(0.0, 0.0));
        wallet.AddReceipt(Receipt());
        Assert.Equal(13.78, wallet.Snapshot().CreditEarned, precision: 6);
    }
}
