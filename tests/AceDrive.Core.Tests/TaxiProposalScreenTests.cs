using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Taxi job proposal page runs headless: the Unity screen's phrase,
/// the fresh-passenger DECLINE roll, ACCEPT handing the candidate to the
/// host, the radius commit rules and voice parity all resolve from the resx
/// and the injected commands.</summary>
public sealed class TaxiProposalScreenTests
{
    // Every draw from this table answers "Solo Sohn and some friends", so
    // the phrase asserts stay exact while the RNG keeps advancing normally.
    private static readonly NamePoolTable SoloTable = new(
    [
        new NamePool("german", 1.0, ["Solo"], ["Sohn"]),
    ],
    ["and some friends"]);

    // 8 firsts x 5 surnames x 2 suffixes: consecutive draws differ, so the
    // regeneration tests can see the swap.
    private static (
        TaxiJobProposalViewModel Vm,
        TestLog Log,
        FakeClock Clock,
        VoiceBeat Beat,
        bool[] Fired, // [0]=back
        List<TaxiJobState> Accepted)
        Create(NamePoolTable? table = null, int seed = 0)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var log = new TestLog();
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var fired = new bool[1];
        var accepted = new List<TaxiJobState>();
        var vm = new TaxiJobProposalViewModel(
            new TaxiJobProposer(table ?? SoloTable, seed), log, beat,
            new RelayCommand(() => fired[0] = true),
            new ParameterizedCommand<TaxiJobState>(job =>
            {
                if (job is not null) accepted.Add(job);
            }));
        return (vm, log, clock, beat, fired, accepted);
    }

    [Fact]
    public void Open_ComposesTheUnityPhrase_HomeAndSecondResidence()
    {
        // SoloTable answers "Solo Sohn" for both residents, so the only
        // possible phrase differs just in leg order: the flip decides whether
        // the home leg or the residence leg leads, exactly like Unity's
        // isCustomerHomePickup. The resx keeps the file's CRLFs, so the
        // comparison normalizes line marks and pins the CONTENT.
        var (vm, _, _, _, _, _) = Create();
        vm.Open();
        string expected = vm.HomeIsPickup
            ? "Solo Sohn and some friends\nneed a ride\n"
                + "from home, Address A\nto the Sohn residence, Address B\n\nAccept this job?"
            : "Solo Sohn and some friends\nneed a ride\n"
                + "from the Sohn residence, Address B\nto home, Address A\n\nAccept this job?";
        Assert.Equal(expected, vm.Phrase.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public void BothFlipSides_LegsFollowTheCoin()
    {
        // Across seeds, both sides of the coin appear, and the committed
        // job's legs always agree with it: home leads when HomeIsPickup,
        // follows otherwise.
        var seen = new HashSet<bool>();
        foreach (int seed in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var (vm, _, _, _, _, accepted) = Create(seed: seed);
            vm.Open();
            seen.Add(vm.HomeIsPickup);
            vm.AcceptCommand.Execute(null);
            TaxiJobState job = Assert.Single(accepted);
            Assert.Equal(vm.HomeIsPickup, job.HomeIsPickup);
            Assert.Equal(vm.HomeIsPickup ? "Address A" : "Address B", job.PickupName);
            Assert.Equal(vm.HomeIsPickup ? "Address B" : "Address A", job.DropoffName);
        }

        Assert.Equal(2, seen.Count);
    }

    [Fact]
    public void Accept_HandsTheCandidate_ToTheHost_AndClosesNothingElse()
    {
        var (vm, _, _, _, fired, accepted) = Create();
        vm.Open();
        vm.AcceptCommand.Execute(null);

        TaxiJobState job = Assert.Single(accepted);
        Assert.Equal(JobPhase.Proposed, job.Phase);
        Assert.Equal("Solo Sohn and some friends", job.Customer.Full);
        // Legs carry the flip's result (their wording is the phrase test's
        // half; BothFlipSides pins the pairing).
        Assert.Equal(vm.HomeIsPickup ? "Address A" : "Address B", job.PickupName);
        Assert.Equal(vm.HomeIsPickup ? "Address B" : "Address A", job.DropoffName);
        // A proposal carries no fare yet (Unity stamps reward at generation):
        // pricing the ride is the host's accept step.
        Assert.Equal(0, job.Fare);
        Assert.False(fired[0]);
    }

    [Fact]
    public void Decline_RollsAFreshPassenger_AndStaysOnThePage()
    {
        var (vm, log, _, _, fired, _) = Create(table: NamePoolTable.Default, seed: 5);
        vm.Open();
        string first = vm.Phrase;

        vm.DeclineCommand.Execute(null);
        Assert.NotEqual(first, vm.Phrase);
        Assert.False(fired[0]);
        Assert.Contains(log.Infos, m => m.Contains("proposal declined", StringComparison.Ordinal));
    }

    [Fact]
    public void Back_ClosesWithoutAJob()
    {
        var (vm, _, _, _, fired, accepted) = Create();
        vm.Open();
        vm.BackCommand.Execute(null);
        Assert.True(fired[0]);
        Assert.Empty(accepted);
    }

    [Fact]
    public void Voice_EveryButtonAnswersToItsText()
    {
        var (vm, _, _, _, _, _) = Create();
        vm.Open();
        Assert.True(vm.VoiceActions.ContainsKey("ACCEPT"));
        Assert.True(vm.VoiceActions.ContainsKey("DECLINE"));
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));
    }

    [Fact]
    public void VoiceAccept_GlowsFirst_AndAcceptsAtTheBeatEnd()
    {
        // Accepting navigates, so the voice tap waits out the shared beat
        // (delivery precedent): teal first, then the commit.
        var (vm, _, clock, beat, _, accepted) = Create();
        vm.Open();
        vm.VoiceActions["ACCEPT"].Invoke();

        Assert.True(vm.AcceptFlash);
        Assert.Empty(accepted);

        clock.Advance(TimeSpan.FromSeconds(0.51));
        beat.Poll();
        Assert.Single(accepted);
        Assert.False(vm.AcceptFlash);
    }

    [Fact]
    public void VoiceBack_GlowsFirst_AndClosesAtTheBeatEnd()
    {
        var (vm, _, clock, beat, fired, _) = Create();
        vm.Open();
        vm.VoiceActions["BACK"].Invoke();

        Assert.True(vm.BackFlash);
        Assert.False(fired[0]);

        clock.Advance(TimeSpan.FromSeconds(0.51));
        beat.Poll();
        Assert.True(fired[0]);
    }

    [Fact]
    public void VoiceDecline_StaysOnThePage_AndActsAtOnce()
    {
        // A page turn, not a navigation: like the row taps on the delivery
        // board, a click and a voice tap act inside the same beat slot.
        var (vm, _, _, _, _, _) = Create(table: NamePoolTable.Default, seed: 5);
        vm.Open();
        string first = vm.Phrase;
        vm.VoiceActions["DECLINE"].Invoke();
        Assert.NotEqual(first, vm.Phrase);
    }

    [Fact]
    public void Radius_CommitsWithUnitysEndEditRules()
    {
        var (vm, log, _, _, _, _) = Create();
        vm.Open();

        // 4.9 km lands on the 5 km floor; the field shows the committed integer.
        vm.RadiusText = "4.9";
        Assert.Equal(5, vm.RadiusKm);
        Assert.Equal("5", vm.RadiusText);

        // Junk restores the committed value (Unity refreshes the field too).
        vm.RadiusText = "zehn";
        Assert.Equal(5, vm.RadiusKm);
        Assert.Equal("5", vm.RadiusText);

        // A comma decimal answers like the typed point.
        vm.RadiusText = "12,4";
        Assert.Equal(12, vm.RadiusKm);
        Assert.Equal("12", vm.RadiusText);

        // Empty field = no limit; the field stays empty, not "0".
        vm.RadiusText = "";
        Assert.Equal(0, vm.RadiusKm);
        Assert.Equal(string.Empty, vm.RadiusText);

        Assert.Contains(log.Infos, m => m.Contains("proposal radius: 5 km", StringComparison.Ordinal));
        Assert.Contains(log.Infos, m => m.Contains("proposal radius cleared", StringComparison.Ordinal));
    }
}
