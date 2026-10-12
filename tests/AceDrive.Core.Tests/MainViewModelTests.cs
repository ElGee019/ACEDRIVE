using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

internal sealed class FakeClock : IClock
{
    public DateTime Now { get; set; } = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);
    public DateTime UtcNow => Now;
    public void Advance(TimeSpan dt) => Now += dt;
}

internal sealed class FakeTelemetry : ITelemetrySource
{
    public bool GameRunning { get; set; } = true;
    public bool PhysicsPresent { get; set; } = true;
    public TelemetrySample Next { get; set; } =
        new(1, 0.5f, 0f, 4, 5000, 0f, 90f);
    public void Dispose() { }
    public TelemetrySample? TryRead() => PhysicsPresent ? Next : null;
}

internal sealed class FakeStore : ISettingsStore
{
    public WindowBounds? Saved { get; private set; }
    public WindowBounds? Load() => null;
    public void Save(WindowBounds bounds) => Saved = bounds;
}

internal sealed class TestLog : ILog
{
    public readonly List<string> Warnings = new();
    public void LogInfo(string message) { }
    public void LogWarning(string message) => Warnings.Add(message);
    public void LogError(string message) { }
}

/// <summary>ViewModel flows run headless against fakes (LSP in action).</summary>
public sealed class MainViewModelTests
{
    private static (MainViewModel Vm, FakeClock Clock, FakeTelemetry Tel, FakeStore Store)
        Create(float speedKmh = 90f)
    {
        // Deterministic locale: resources resolve to en, numbers format with '.'.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var tel = new FakeTelemetry();
        tel.Next = tel.Next with { SpeedKmh = speedKmh };
        var store = new FakeStore();
        var vm = new MainViewModel(tel, clock, new TaxiShift(), store,
            new NullLogger(), FareTable.Default);
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
    public void Pickup_OnlyFromProposed()
    {
        var (vm, _, _, _) = Create();
        Assert.False(vm.PickupCommand.CanExecute(null));
        vm.NewJobCommand.Execute(null);
        Assert.True(vm.PickupCommand.CanExecute(null));
    }

    [Fact]
    public void ToggleLock_FlipsHint()
    {
        var (vm, _, _, _) = Create();
        Assert.True(vm.IsLocked);
        string before = vm.HintText;
        vm.ToggleLockCommand.Execute(null);
        Assert.False(vm.IsLocked);
        Assert.NotEqual(before, vm.HintText);
    }
}
