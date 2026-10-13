using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>ViewModel flows run headless against fakes (LSP in action).</summary>
public sealed class MainViewModelTests
{
    internal static readonly OnboardingContent EmptyContent = new([], new Dictionary<string, string[]>(), [], []);
    private static (MainViewModel Vm, FakeClock Clock, FakeTelemetry Tel, FakeStore Store)
        Create(float speedKmh = 90f, OnboardingState? progress = null, DeliveryCatalog? catalog = null)
    {
        // Deterministic locale: resources resolve to en, numbers format with '.'.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var tel = new FakeTelemetry();
        tel.Next = tel.Next with { SpeedKmh = speedKmh };
        var store = new FakeStore();
        var onboarding = new FakeOnboardingStore();
        if (progress is not null)
        {
            onboarding.Save(progress);
        }
        var vm = new MainViewModel(tel, clock, new TaxiShift(), store,
            new NullLogger(), FareTable.Default,
            onboarding, EmptyContent, new FakeVoiceListener(available: false),
            new FakeCarBrandSource(), TodoTable.Default, catalog ?? DeliveryCatalog.Default);
        return (vm, clock, tel, store);
    }

    [Fact]
    public void NoGame_ShowsWaiting()
    {
        var (vm, _, tel, _) = Create();
        tel.GameRunning = false;
        tel.PhysicsPresent = false;
        vm.Poll();
        Assert.False(vm.IsLive);
        Assert.Equal("0 km/h", vm.SpeedText);
    }

    [Fact]
    public void LiveTelemetry_UpdatesDisplay()
    {
        var (vm, _, tel, _) = Create();
        tel.Next = tel.Next with { PacketId = 7, Rpm = 6000, Gear = 4, Gas = 0.8f };
        vm.Poll();
        Assert.True(vm.IsLive);
        Assert.Equal("90 km/h", vm.SpeedText);
        Assert.Equal("3", vm.GearText); // gear 4 -> 3rd
    }

    [Fact]
    public void EnRoute_DrivingAccumulatesFare()
    {
        // 180 km/h x 50 polls x 0.4 s = 20 s = 1.0 km exactly.
        var (vm, clock, _, _) = Create(speedKmh: 180f);
        vm.NewJobCommand.Execute(null);
        vm.PickupCommand.Execute(null);
        for (int i = 0; i < 50; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(400));
            vm.Poll();
        }
        // 3.50 + 1.80 * 1.0 = 5.30
        Assert.Contains("1.0 km", vm.JobLine, StringComparison.Ordinal);
        Assert.Equal("€5.30", vm.FareText);
    }

    [Fact]
    public void Dropoff_RecordsShiftWithTip()
    {
        var (vm, clock, _, _) = Create(speedKmh: 0f);
        vm.NewJobCommand.Execute(null);
        vm.PickupCommand.Execute(null);
        clock.Advance(TimeSpan.FromMilliseconds(100));
        vm.Poll();
        vm.DropoffCommand.Execute(null);
        // fare 3.50, tip 10% = 0.35
        Assert.Equal("€3.85", vm.FareText);
        Assert.Contains("1 jobs", vm.ShiftText, StringComparison.Ordinal);
    }

    [Fact]
    public void NewJob_StartsShift_ShowsCountdownAndContinueLabel()
    {
        var (vm, _, _, _) = Create(speedKmh: 0f);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);

        vm.NewJobCommand.Execute(null);
        Assert.Equal("CONTINUE TAXI SHIFT", control.TaxiLabel);
        Assert.Contains("left", vm.ShiftText, StringComparison.Ordinal);
        Assert.True(control.VoiceActions.ContainsKey("CONTINUE TAXI SHIFT"));
    }

    [Fact]
    public void ShiftTimeout_EndsShift_FlipsLabelBack()
    {
        var (vm, clock, _, _) = Create(speedKmh: 0f);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        vm.NewJobCommand.Execute(null);
        Assert.Equal("CONTINUE TAXI SHIFT", control.TaxiLabel);

        clock.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1)));
        vm.Poll();
        Assert.Equal("START TAXI SHIFT", control.TaxiLabel);
        Assert.DoesNotContain("left", vm.ShiftText, StringComparison.Ordinal);
    }

    [Fact]
    public void Pickup_OnlyFromProposed()
    {
        var (vm, _, _, _) = Create();
        Assert.False(vm.PickupCommand.CanExecute(null));
        vm.NewJobCommand.Execute(null);
        Assert.True(vm.PickupCommand.CanExecute(null));
    }

    [Fact]
    public void ToggleShow_FlipsVisibility()
    {
        var (vm, _, _, _) = Create();
        Assert.True(vm.IsShown);
        vm.ToggleShowCommand.Execute(null);
        Assert.False(vm.IsShown);
    }

    [Fact]
    public void FinishDialogue_ShowsControlPage()
    {
        var (vm, _, _, _) = Create();
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.Equal("Monday 12:00 | Week 1", control.DateHeader);
        Assert.False(vm.IsOnboarding);
    }

    [Fact]
    public void CompletedOnboarding_BootsToControlPage()
    {
        var (vm, _, _, _) = Create(progress: OnboardingState.Fresh with { BirthdayDone = true });
        Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
    }

    [Fact]
    public void DeliveryButton_OpensCareerPage_BackReturns()
    {
        var catalog = new DeliveryCatalog(
            [new DeliveryStore("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, 140, 1)],
            0.7, 100);
        var (vm, _, _, _) = Create(catalog: catalog);
        vm.FinishDialogue();
        var control = Assert.IsType<MainControlScreenViewModel>(vm.CurrentScreen);
        Assert.True(control.DeliveryCommand.CanExecute(null));

        control.DeliveryCommand.Execute(null);
        var delivery = Assert.IsType<DeliveryJobScreenViewModel>(vm.CurrentScreen);
        Assert.False(vm.IsOnboarding); // job page, not onboarding
        Assert.Equal("OPEN NEW CAREER", delivery.MainButtonLabel);

        delivery.ToggleOrderCommand.Execute(delivery.Rows[0]);
        Assert.Equal("DELIVER", delivery.MainButtonLabel);

        // Back with a pending cart = CANCEL ORDER: refunds, then exits.
        delivery.BackCommand.Execute(null);
        Assert.Same(control, vm.CurrentScreen);

        // The page and its now-empty cart survive navigation.
        control.DeliveryCommand.Execute(null);
        Assert.Same(delivery, vm.CurrentScreen);
        Assert.Equal("OPEN NEW CAREER", delivery.MainButtonLabel);
    }
}
