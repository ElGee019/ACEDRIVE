using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The real taxi route for headless flows: proposal ACCEPT, pickup
/// confirmation, the customer request (accepted when one opens), the
/// passenger leg, drop-off. The meter deliberately ignores ticks of a second
/// or more (the stalled-poll guard), so the drive is fed in half-second steps
/// at 360 km/h: 0.05 km per tick, 8.2 km in 164 ticks.
/// </summary>
internal static class TaxiFlow
{
    public const double DriveTickSeconds = 0.5;
    public const float DriveSpeedKmh = 360f;
    private const double KmPerTick = DriveSpeedKmh * DriveTickSeconds / 3600.0;
    public const double DefaultDistanceKm = 8.2;

    public static TaxiJobProposalViewModel Propose(MainViewModel vm)
    {
        vm.TaxiShiftCommand.Execute(null);
        var proposal = Assert.IsType<TaxiJobProposalViewModel>(vm.CurrentScreen);
        proposal.AcceptCommand.Execute(null);
        return proposal;
    }

    /// <summary>
    /// Confirms the pickup and waits out the drop-off task text's read-in
    /// delay, so a rolled request reaches its page. Returns that page, or null
    /// when the ride has no ask.
    /// </summary>
    public static CustomerRequestScreenViewModel? Pickup(
        MainViewModel vm, FakeClock clock)
    {
        vm.PickupCommand.Execute(null);
        clock.Advance(TimeSpan.FromSeconds(TaxiRules.Default.RequestDelaySeconds));
        vm.Poll();
        return vm.CurrentScreen as CustomerRequestScreenViewModel;
    }

    /// <summary>Accepts the first request option (passive/speed ACCEPT; a
    /// quiz's first value, which a quiz test must drive itself) and closes
    /// the page. No-request rides pass straight through.</summary>
    public static void AnswerRequest(MainViewModel vm)
    {
        if (vm.CurrentScreen is not CustomerRequestScreenViewModel request)
        {
            return;
        }

        if (request.Options.Count > 0)
        {
            request.Options[0].Command.Execute(null);
        }

        request.BackCommand.Execute(null);
    }

    public static void Drive(
        MainViewModel vm, FakeClock clock, FakeTelemetry tel,
        double km = DefaultDistanceKm)
    {
        tel.Next = tel.Next with { SpeedKmh = DriveSpeedKmh };
        int ticks = (int)Math.Round(km / KmPerTick);
        for (int i = 0; i < ticks; i++)
        {
            clock.Advance(TimeSpan.FromSeconds(DriveTickSeconds));
            vm.Poll();
        }
    }

    public static void AcceptAndDrive(
        MainViewModel vm, FakeClock clock, FakeTelemetry tel,
        double km = DefaultDistanceKm)
    {
        Propose(vm);
        Pickup(vm, clock);
        AnswerRequest(vm);
        Drive(vm, clock, tel, km);
        vm.DropoffCommand.Execute(null);
    }
}
