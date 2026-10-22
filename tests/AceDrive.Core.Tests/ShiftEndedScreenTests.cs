using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The shift-ended page runs headless: the finished shift, the
/// lifetime records and the best-shift board, then CONTINUE hands back.</summary>
public sealed class ShiftEndedScreenTests
{
    private static (ShiftEndedScreenViewModel Vm, bool[] Continued, FakeShiftStandings Board) Create(
        ShiftSummary? summary = null, ShiftRecord? record = null,
        FakeShiftStandings? board = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var continued = new bool[1];
        FakeShiftStandings standings = board ?? new FakeShiftStandings();
        var vm = new ShiftEndedScreenViewModel(
            summary ?? new ShiftSummary(Jobs: 4, Earnings: 123.45, DurationMinutes: 47),
            record ?? Recorded(),
            standings,
            new VoiceBeat(new FakeClock()),
            new RelayCommand(() => continued[0] = true));
        return (vm, continued, standings);
    }

    private static ShiftRecord Recorded()
    {
        var record = new ShiftRecord(null);
        record.Complete(jobs: 9, earnings: 500.0, firstEarnedTicks: 1);
        record.Complete(jobs: 4, earnings: 123.45, firstEarnedTicks: 2);
        return record;
    }

    [Fact]
    public void Lines_ShowTheShiftAndTheLifetimeRecords()
    {
        var (vm, _, _) = Create();

        Assert.Equal("Jobs completed: 4", vm.JobsLine);
        Assert.Equal("Total earnings: €123.45", vm.EarningsLine);
        Assert.Equal("Shift duration: 47 min", vm.DurationLine);
        Assert.Equal("Longest shift: 9 jobs", vm.LongestLine);
        Assert.Equal("Highest earnings: €500.00", vm.BestLine);
        Assert.Equal("Total shifts completed: 2", vm.TotalShiftsLine);
    }

    [Fact]
    public void Standings_UnknownUntilTheBoardAnswers()
    {
        var (vm, _, board) = Create();

        Assert.Equal("Your shift world standings is —/—.", vm.Standings.Line);
        Assert.Equal(string.Empty, vm.Standings.DeltaLine);
        Assert.False(vm.Standings.Changed);

        // The boot board lands while the page is up: the block follows it.
        board.Publish(new ShiftStandingsResult(3, 20, 2, 5, false));

        Assert.Equal("Your shift world standings is 3rd of 20.", vm.Standings.Line);
        Assert.Equal("You gained 2 positions.", vm.Standings.DeltaLine);
        Assert.True(vm.Standings.Changed);
        Assert.Equal(1, vm.Standings.PulseSignal);
    }

    [Fact]
    public void Continue_UnhooksTheBoardAndHandsBack()
    {
        var (vm, continued, board) = Create();
        board.Publish(new ShiftStandingsResult(3, 20, 0, 3, false));

        vm.ContinueCommand.Execute(null);

        Assert.True(continued[0]);
        Assert.False(vm.ContinueFlash);
        // The page is gone: a late board result must not touch it anymore.
        int pulses = vm.Standings.PulseSignal;
        board.Publish(new ShiftStandingsResult(2, 20, 1, 3, false));
        Assert.Equal(pulses, vm.Standings.PulseSignal);
        Assert.Equal("Your shift world standings is 3rd of 20.", vm.Standings.Line);
    }

    [Fact]
    public void VoiceContinue_GlowsThenContinuesAfterTheBeat()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        bool continued = false;
        var vm = new ShiftEndedScreenViewModel(
            new ShiftSummary(Jobs: 1, Earnings: 10.0, DurationMinutes: 5),
            new ShiftRecord(null), new FakeShiftStandings(), beat,
            new RelayCommand(() => continued = true));

        Assert.True(vm.VoiceActions.TryGetValue("CONTINUE", out Action? act));
        act!();

        Assert.True(vm.ContinueFlash);
        Assert.False(continued);

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();

        Assert.True(continued);
    }
}
