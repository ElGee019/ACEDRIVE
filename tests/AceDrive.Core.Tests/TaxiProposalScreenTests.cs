using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
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
        Create(
            NamePoolTable? table = null, int seed = 0, AddressMap? map = null,
            AddressLocation? home = null, IResidentStore? residents = null,
            int seats = 4)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var log = new TestLog();
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var fired = new bool[1];
        var accepted = new List<TaxiJobState>();
        AddressMap addressMap = map ?? AddressMap.Default;
        var vm = new TaxiJobProposalViewModel(
            TaxiTestData.Proposer(
                map: addressMap, table: table ?? SoloTable, seed: seed,
                residents: residents),
            addressMap, () => home, () => seats, log, beat,
            new RelayCommand(() => fired[0] = true),
            new ParameterizedCommand<TaxiJobState>(job =>
            {
                if (job is not null) accepted.Add(job);
            }));
        return (vm, log, clock, beat, fired, accepted);
    }

    // Mirrors the production door line: the shared street shape plus the
    // map's city (ProposalAddress). Used to build phrase expectations from the
    // accepted job instead of hardcoding a random house.
    private static string Door(AddressLocation? address) =>
        Strings.Format(
            "ProposalAddress",
            AddressFormatter.Format(address!.Value),
            AddressMap.Default.City);

    [Fact]
    public void Open_ComposesTheUnityPhrase_HomeAndSecondResidence()
    {
        // SoloTable answers "Solo Sohn" for both residents; the flip decides
        // whether the home leg or the residence leg leads. The addresses come
        // from the accepted job, so the assertion never hardcodes a random
        // house. The resx keeps the file's CRLFs, so the comparison
        // normalizes line marks and pins the CONTENT.
        var (vm, _, _, _, _, accepted) = Create();
        vm.Open();
        vm.AcceptCommand.Execute(null);
        TaxiJobState job = Assert.Single(accepted);

        string home = Door(job.CustomerHome);
        string residence = Door(job.HomeIsPickup ? job.DropoffAddress : job.PickupAddress);
        string expected = vm.HomeIsPickup
            ? $"Solo Sohn and some friends\nneed a ride from home,\n{home}\n"
                + $"to the Sohn residence, {residence}\n\nAccept this job?"
            : $"Solo Sohn and some friends\n"
                + $"need a ride from the Sohn residence,\n{residence}\n"
                + $"to home, {home}\n\nAccept this job?";
        Assert.Equal(expected, vm.Phrase.Replace("\r\n", "\n", StringComparison.Ordinal));
        // The formatter is shared with the expectation, so pin the content it
        // must have produced: two different map streets plus the city.
        Assert.Contains("Street 1", vm.Phrase, StringComparison.Ordinal);
        Assert.Contains("Street 2", vm.Phrase, StringComparison.Ordinal);
        Assert.Contains(", Brands", vm.Phrase, StringComparison.Ordinal);
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
            Assert.NotNull(job.PickupAddress);
            Assert.NotNull(job.DropoffAddress);
            Assert.NotEqual(
                job.PickupAddress!.Value.StreetName,
                job.DropoffAddress!.Value.StreetName);
            Assert.Equal(Door(job.PickupAddress), job.PickupName);
            Assert.Equal(Door(job.DropoffAddress), job.DropoffName);
            Assert.True(job.CustomerHome == job.PickupAddress
                || job.CustomerHome == job.DropoffAddress);
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
        // Legs carry the flip's result and the structured doors (BothFlipSides
        // pins the pairing).
        Assert.NotNull(job.CustomerHome);
        Assert.NotNull(job.PickupAddress);
        Assert.NotNull(job.DropoffAddress);
        Assert.Equal(Door(job.PickupAddress), job.PickupName);
        Assert.Equal(Door(job.DropoffAddress), job.DropoffName);
        // A proposal carries no fare yet (Unity stamps reward at generation):
        // pricing the ride is the host's accept step.
        Assert.Equal(0, job.Fare);
        Assert.False(fired[0]);
    }

    [Fact]
    public void SingleSeatCar_ShowsAndCommitsTheCustomerAlone()
    {
        var (vm, _, _, _, _, accepted) = Create(seats: 1);
        vm.Open();

        // One seat, one customer: the suffix-triggered companion group has no
        // room in this car, so neither the page nor the job names it.
        string phrase = vm.Phrase.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.StartsWith("Solo Sohn\nneed a ride from ", phrase, StringComparison.Ordinal);
        Assert.DoesNotContain("and some friends", phrase, StringComparison.Ordinal);

        vm.AcceptCommand.Execute(null);
        TaxiJobState job = Assert.Single(accepted);
        Assert.Equal("Solo Sohn", job.Customer.Full);
        Assert.Empty(job.Customer.Suffix);
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

    // 20 km loop, two 10 km streets, one house every km: the 5 km floor can
    // actually filter here, unlike the 1,929 m Brands Hatch stand-in.
    private static readonly AddressMap LongMap = new(
        "Germany", "Test", 20_000.0, 1_000.0,
        [
            new AddressStreet("a", "A", 0.0, 0.5),
            new AddressStreet("b", "B", 0.5, 1.0),
        ]);

    [Fact]
    public void Radius_RegeneratesAProposalOutsideTheLimit()
    {
        var home = new AddressLocation("A", 1, HouseSide.Right);
        int seed = OffRadiusSeed(home);
        var (vm, _, _, _, _, accepted) = Create(
            seed: seed, map: LongMap, home: home);
        vm.Open();
        string before = vm.Phrase;

        vm.RadiusText = "5";

        Assert.Equal(5, vm.RadiusKm);
        Assert.NotEqual(before, vm.Phrase);
        vm.AcceptCommand.Execute(null);
        TaxiJobState job = Assert.Single(accepted);
        Assert.True(AddressBook.DistanceMeters(
            LongMap, home, job.PickupAddress!.Value) <= 5_000.0);
        Assert.True(AddressBook.DistanceMeters(
            LongMap, home, job.DropoffAddress!.Value) <= 5_000.0);
    }

    [Fact]
    public void OneStreetMap_FallsBackToThePlaceholderLegs()
    {
        var map = new AddressMap(
            "Germany", "Brands", 1_929.0, 30.0,
            [new AddressStreet("only", "Only Street", 0.0, 1.0)]);
        var (vm, _, _, _, _, accepted) = Create(map: map);
        vm.Open();

        Assert.Contains("Address A", vm.Phrase, StringComparison.Ordinal);
        Assert.Contains("Address B", vm.Phrase, StringComparison.Ordinal);

        vm.AcceptCommand.Execute(null);
        TaxiJobState job = Assert.Single(accepted);
        Assert.Null(job.CustomerHome);
        Assert.Null(job.PickupAddress);
        Assert.Null(job.DropoffAddress);
    }

    // The first draw of an unlimited proposal is the one the page shows, so
    // finding an off-radius seed guarantees the commit has something to swap.
    private static int OffRadiusSeed(AddressLocation home)
    {
        for (int seed = 0; seed < 100; seed++)
        {
            TaxiProposal proposal = TaxiTestData.Proposer(
                map: LongMap, table: SoloTable, seed: seed)
                .Next(passengerCapacity: 4);
            if (AddressBook.DistanceMeters(
                    LongMap, home, proposal.CustomerHome!.Value) > 5_000.0
                || AddressBook.DistanceMeters(
                    LongMap, home, proposal.SecondResidence!.Value) > 5_000.0)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("no off-radius draw in 100 seeds");
    }
}
