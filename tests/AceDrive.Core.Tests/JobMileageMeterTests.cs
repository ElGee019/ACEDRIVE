using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>The event-driven odometer bank (PORTPLAN 44): a job's start and
/// settle are the only two reads, and a crash folds the clean part in before
/// the run resets.</summary>
public sealed class JobMileageMeterTests
{
    private static readonly CarSpec Car =
        new("ks_meter_car", 2020, "Test", "Meter", Class: 3, Price: 40000.0, TopSpeedKmh: 240);

    [Fact]
    public void FinishJob_BanksTheOdometerDeltaOnce()
    {
        var garage = GarageWithCar(out OwnedCar car);
        var wallet = new DriverWallet();
        var meter = new JobMileageMeter(() => garage.Current, wallet, new Random(1));

        meter.StartJob();
        car.Drive(8.2);
        Assert.Equal(8.2, meter.FinishJob(), precision: 6);

        Assert.Equal(8.2, wallet.JobDistanceKm, precision: 6);
        Assert.Equal(8.2, wallet.LongestAccidentFreeKm, precision: 6);
        // The start reading is cleared: a second finish on the same job is a
        // no-op instead of banking the odometer twice.
        Assert.Equal(0.0, meter.FinishJob(), precision: 9);
        Assert.Equal(8.2, wallet.JobDistanceKm, precision: 6);
    }

    [Fact]
    public void CancelJob_BanksNothing()
    {
        var garage = GarageWithCar(out OwnedCar car);
        var wallet = new DriverWallet();
        var meter = new JobMileageMeter(() => garage.Current, wallet, new Random(1));

        meter.StartJob();
        car.Drive(3.0);
        meter.CancelJob();
        meter.FinishJob();

        Assert.Equal(0.0, wallet.JobDistanceKm, precision: 9);
        Assert.Equal(0.0, wallet.AccidentFreeKm, precision: 9);
    }

    [Fact]
    public void RegisterAccident_BanksTheCleanPartAndRestartsTheRun()
    {
        var garage = GarageWithCar(out OwnedCar car);
        var wallet = new DriverWallet();
        var meter = new JobMileageMeter(() => garage.Current, wallet, new Random(1));

        meter.StartJob();
        car.Drive(4.0);
        meter.RegisterAccident();

        // The four clean kilometres count, the record takes the crash, and the
        // live run starts again at the current odometer for a job that survives.
        Assert.Equal(4.0, wallet.JobDistanceKm, precision: 6);
        Assert.Equal(4.0, wallet.LongestAccidentFreeKm, precision: 6);
        Assert.Equal(0.0, wallet.AccidentFreeKm, precision: 9);
        Assert.Equal(1, wallet.Accidents);

        car.Drive(2.0);
        meter.FinishJob();

        Assert.Equal(6.0, wallet.JobDistanceKm, precision: 6);
        Assert.Equal(2.0, wallet.AccidentFreeKm, precision: 6);
        Assert.Equal(4.0, wallet.LongestAccidentFreeKm, precision: 6);
    }

    [Fact]
    public void ANewCar_InvalidatesThePendingPair()
    {
        var first = new CarSpec("ks_first", 2020, "Test", "First", Class: 3, Price: 40000.0);
        var second = new CarSpec("ks_second", 2021, "Test", "Second", Class: 3, Price: 40000.0);
        var garage = new Garage([first, second]);
        garage.Add(first, week: 1);
        var wallet = new DriverWallet();
        var meter = new JobMileageMeter(() => garage.Current, wallet, new Random(1));

        meter.StartJob();
        garage.Add(second, week: 2);
        garage.SetCurrent(second.Key);
        if (garage.Current is { } car)
        {
            car.Drive(5.0);
        }

        Assert.Equal(0.0, meter.FinishJob(), precision: 9);
        Assert.Equal(0.0, wallet.JobDistanceKm, precision: 9);
    }

    [Fact]
    public void FinishJob_WithoutAnOwnedCar_BanksNothing()
    {
        var wallet = new DriverWallet();
        var meter = new JobMileageMeter(() => null, wallet, new Random(1));

        meter.StartJob();
        Assert.Equal(0.0, meter.FinishJob(), precision: 9);
        Assert.Equal(0.0, wallet.JobDistanceKm, precision: 9);
    }

    private static Garage GarageWithCar(out OwnedCar car)
    {
        var garage = new Garage([Car]);
        car = garage.Add(Car, week: 1);
        return garage;
    }
}
