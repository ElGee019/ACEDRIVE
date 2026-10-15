using AceDrive.Core;

namespace AceDrive.Core.Tests;

public sealed class DriverWalletTests
{
    private static TaxiReceipt Receipt(double total = 13.78, double xp = 0.082) =>
        new(8.2, 1, [], 43.15, 10.0, 53.15, 10.63, 1, 50.0, total, xp, 1, 1, 100, "", 0, false);

    [Fact]
    public void Fresh_StartsEmpty()
    {
        var wallet = new DriverWallet();
        Assert.Equal(0.0, wallet.Cash);
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
        var wallet = new DriverWallet();
        wallet.AddReceipt(Receipt());
        wallet.AddReceipt(Receipt());
        Assert.Equal(27.56, wallet.Cash);
        Assert.Equal(0.164, wallet.Xp, precision: 9);
    }

    [Fact]
    public void Snapshot_RoundTrips()
    {
        var wallet = new DriverWallet(new DriverState(100.0, 5.0));
        wallet.AddReceipt(Receipt());
        DriverState snapshot = wallet.Snapshot();
        Assert.Equal(113.78, snapshot.Cash);
        Assert.Equal(5.082, snapshot.Xp, precision: 9);
    }
}
