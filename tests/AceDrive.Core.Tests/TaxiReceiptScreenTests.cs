using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>Taxi receipt page runs headless: every Unity line resolves from
/// the settled receipt and the resx, the rivals block follows the standings
/// backend, CONTINUE hands back to the host.</summary>
public sealed class TaxiReceiptScreenTests
{
    private static (TaxiReceiptScreenViewModel Vm, bool[] Continued, FakeRivals Rivals) Create(
        FakeRivals? rivals = null, FakeCarRarity? rarity = null, string carKey = "")
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var continued = new bool[1];
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, capacity: 1, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: true, fines: 1,
            FareTable.Default, currentXp: 0.0, carKey: carKey);
        // Empty wallet: the page lines must show the settle alone, not the
        // fresh-start seed (covered in DriverWalletTests).
        var wallet = new DriverWallet(new DriverState(0.0, 0.0));
        wallet.AddReceipt(receipt);
        var shift = new TaxiShift();
        shift.RecordCompleted(
            new TaxiJobState { Phase = JobPhase.Complete, Fare = 53.15, Tip = 10.63, Fine = 50.0 },
            clock.UtcNow);
        var fake = rivals ?? new FakeRivals();
        var vm = new TaxiReceiptScreenViewModel(
            receipt, "Solo Sohn", wallet, shift, beat,
            new RelayCommand(() => continued[0] = true), fake,
            rarity ?? new FakeCarRarity());
        return (vm, continued, fake);
    }

    [Fact]
    public void Lines_ShowTheSettledRide()
    {
        var (vm, _, _) = Create();
        Assert.Equal("Job distance: 8.2 km", vm.DistanceLine);
        Assert.Equal("Customers: 1. Solo Sohn", vm.CustomerLine);
        Assert.Equal(
            [new ReceiptLine("0.0–2.0 km @ €6.67/km: 2.0 km", "€13.34"),
             new ReceiptLine("2.0–5.0 km @ €5.67/km: 3.0 km", "€17.01"),
             new ReceiptLine("5.0–10.0 km @ €4.00/km: 3.2 km", "€12.80")],
            vm.TierLines);
        Assert.Equal(new ReceiptLine("Forfait", "€10.00"), vm.ForfaitLine);
        Assert.Equal(new ReceiptLine("Fare: 1 × €43.15", "€43.15"), vm.FareLine);
        Assert.True(vm.HasTip);
        Assert.Equal(new ReceiptLine("Customer tip", "€10.63"), vm.TipLine);
        Assert.True(vm.HasFine);
        Assert.Equal(new ReceiptLine("Speed fine", "−€50.00"), vm.FineLine);
        Assert.Equal(new ReceiptLine("Total earned", "€13.78"), vm.TotalLine);
        Assert.Equal("Wallet: €13.78", vm.WalletLine);
        Assert.Equal("Shift: 1 jobs · €13.78", vm.ShiftLine);
        Assert.Equal("XP earned: 0.08", vm.XpLine);
        Assert.Equal("XP: (8.2 km × class 1 × rarity 1) / 100 × street knowledge 100%", vm.XpCalcLine);
        Assert.Equal("Total XP: 0.08", vm.TotalXpLine);
    }

    [Fact]
    public void WithoutFine_LineStaysEmpty()
    {
        // No fine means no mention: the receipt carries a blank fine line
        // (the view keeps its space without collapsing), never a zero fine.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, capacity: 1, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: true, fines: 0,
            FareTable.Default, currentXp: 0.0);
        var vm = new TaxiReceiptScreenViewModel(
            receipt, "Solo Sohn", new DriverWallet(), new TaxiShift(), beat,
            new RelayCommand(() => { }), new FakeRivals(), new FakeCarRarity());
        Assert.False(vm.HasFine);
        Assert.Equal(new ReceiptLine(string.Empty, string.Empty), vm.FineLine);
        Assert.Equal(new ReceiptLine("Total earned", "€63.78"), vm.TotalLine);
    }

    [Fact]
    public void WithoutTip_LineStaysEmpty()
    {
        // No customer tip means no tip row: the view collapses it and the
        // line itself carries no zero amount (Unity hides the text too).
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, capacity: 1, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: false, fines: 0,
            FareTable.Default, currentXp: 0.0);
        var vm = new TaxiReceiptScreenViewModel(
            receipt, "Solo Sohn", new DriverWallet(), new TaxiShift(), beat,
            new RelayCommand(() => { }), new FakeRivals(), new FakeCarRarity());
        Assert.False(vm.HasTip);
        Assert.Equal(new ReceiptLine(string.Empty, string.Empty), vm.TipLine);
    }

    [Fact]
    public void Continue_HandsBackToHost()
    {
        var (vm, continued, _) = Create();
        vm.ContinueCommand.Execute(null);
        Assert.True(continued[0]);
    }

    [Fact]
    public void VoiceContinue_AnswersToItsLabel()
    {
        var (vm, continued, _) = Create();
        Assert.True(vm.VoiceActions.TryGetValue(Strings.Get("ReceiptContinue"), out Action? act));
        act!();
        // Navigating taps wait out the shared beat; the click path acts now.
        Assert.False(continued[0]);
        vm.ContinueCommand.Execute(null);
        Assert.True(continued[0]);
    }

    [Fact]
    public void WithoutFriendship_BoxHidden()
    {
        var (vm, _, _) = Create();
        Assert.False(vm.HasFriendship);
        Assert.Equal(string.Empty, vm.FriendshipLine);
        Assert.False(vm.FriendshipHighlight);
    }

    [Fact]
    public void WithoutBrandBonus_SponsorshipHidden()
    {
        var (vm, _, _) = Create();
        Assert.False(vm.HasBrandBonus);
    }

    [Fact]
    public void WithBrandBonus_ShowsSponsorshipLines()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, capacity: 1, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: true, fines: 1,
            FareTable.Default, currentXp: 0.0,
            brand: "Mazda", brandDistanceKm: 8.2);
        var vm = new TaxiReceiptScreenViewModel(
            receipt, "Solo Sohn", new DriverWallet(), new TaxiShift(), beat,
            new RelayCommand(() => { }), new FakeRivals(), new FakeCarRarity());
        Assert.True(vm.HasBrandBonus);
        Assert.Equal(new ReceiptLine("Mazda Sponsorship", "€2.66"), vm.SponsorshipLine);
        Assert.Equal(
            "Mazda sponsorship: 5.00% for driving 8.20km in their cars.",
            vm.BrandBonusLine);
        Assert.Equal(new ReceiptLine("Total earned", "€16.44"), vm.TotalLine);
    }

    [Fact]
    public void WithStarterBonus_ShowsTheDecayedRow()
    {
        // 1.5 km zone = 99%, halved by the second car = 49.5% of 13.78 =
        // 6.82 on top, total 20.60.
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, capacity: 1, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: true, fines: 1,
            FareTable.Default, currentXp: 0.0, zoneKm: 1.5, ownedCars: 2);
        var vm = new TaxiReceiptScreenViewModel(
            receipt, "Solo Sohn", new DriverWallet(), new TaxiShift(), beat,
            new RelayCommand(() => { }), new FakeRivals(), new FakeCarRarity());
        Assert.True(vm.HasStarterBonus);
        Assert.Equal(
            new ReceiptLine("Business Starter Bonus 49.5%", "€6.82"),
            vm.StarterBonusLine);
        Assert.Equal(new ReceiptLine("Total earned", "€20.60"), vm.TotalLine);
    }

    [Fact]
    public void WithoutStarterBonus_RowHidden()
    {
        var (vm, _, _) = Create();
        Assert.False(vm.HasStarterBonus);
    }

    [Fact]
    public void WithFriendship_ShowsTierSentence()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        TaxiReceipt receipt = TaxiReceiptBuilder.Build(
            8.2, capacity: 1, carClass: 1, carRarity: 1,
            streetKnowledgePct: 100, tipEligible: true, fines: 1,
            FareTable.Default, currentXp: 0.0,
            new FriendshipResult("Finn Marsh", 5, true));
        var vm = new TaxiReceiptScreenViewModel(
            receipt, "Finn Marsh", new DriverWallet(), new TaxiShift(), beat,
            new RelayCommand(() => { }), new FakeRivals(), new FakeCarRarity());
        Assert.True(vm.HasFriendship);
        Assert.Equal("New familiarity with Finn: 5. Finn is a friend.", vm.FriendshipLine);
        Assert.True(vm.FriendshipHighlight);
    }

    [Fact]
    public void Standings_KnownResult_ShowsOrdinalAndGain()
    {
        // The host (MainViewModel) refreshes before opening the page, so
        // the receipt constructor sees the fresh result already in LastResult.
        var rivals = new FakeRivals
        {
            Result = new RivalsResult(3, 24, 2, 5, false),
        };
        var (vm, _, _) = Create(rivals);
        Assert.Equal("Your Global XP Rank is 3rd of 24.", vm.Standings.Line);
        Assert.Equal("You gained 2 positions.", vm.Standings.DeltaLine);
        Assert.True(vm.Standings.Changed);
    }

    [Fact]
    public void Standings_LostOne_UsesSingularKey()
    {
        var (vm, _, rivals) = Create();
        rivals.Emit(new RivalsResult(4, 40, -1, 3, false));
        Assert.Equal("Your Global XP Rank is 4th of 40.", vm.Standings.Line);
        Assert.Equal("You lost 1 position.", vm.Standings.DeltaLine);
        Assert.True(vm.Standings.Changed);
    }

    [Fact]
    public void Standings_NoChange_WhiteQuietLine()
    {
        var (vm, _, rivals) = Create();
        rivals.Emit(new RivalsResult(11, 57, 0, 11, false));
        // 11–13 keep the -th suffix; change 0 means no teal, no pulse.
        Assert.Equal("Your Global XP Rank is 11th of 57.", vm.Standings.Line);
        Assert.Equal("No change in standings.", vm.Standings.DeltaLine);
        Assert.False(vm.Standings.Changed);
        Assert.Equal(0, vm.Standings.PulseSignal);
    }

    [Fact]
    public void Standings_EveryRankMoveRestartsThePulse()
    {
        // Unity stops any running pulse and starts a new one for every
        // result that moved the rank, so a live board update under the open
        // receipt must advance the signal again.
        var rivals = new FakeRivals
        {
            Result = new RivalsResult(5, 20, 1, 6, false),
        };
        var (vm, _, _) = Create(rivals);
        Assert.Equal(1, vm.Standings.PulseSignal);

        rivals.Emit(new RivalsResult(3, 20, 2, 5, false));
        Assert.Equal(2, vm.Standings.PulseSignal);
        Assert.True(vm.Standings.Changed);
    }

    [Fact]
    public void Standings_NoChangeCancelsAnInFlightPulse()
    {
        var rivals = new FakeRivals
        {
            Result = new RivalsResult(5, 20, 1, 6, false),
        };
        var (vm, _, _) = Create(rivals);
        Assert.Equal(1, vm.Standings.PulseSignal);

        rivals.Emit(new RivalsResult(5, 20, 0, 5, false));
        Assert.Equal(0, vm.Standings.PulseSignal);
        Assert.False(vm.Standings.Changed);
    }

    [Fact]
    public void Standings_NothingKnown_PlaceholderUntilTheBoardLands()
    {
        var (vm, _, rivals) = Create();
        // Unity's else-branch: —/— with an empty delta, no pulse.
        Assert.Equal("Your Global XP Rank is —/—.", vm.Standings.Line);
        Assert.Equal(string.Empty, vm.Standings.DeltaLine);
        Assert.False(vm.Standings.Changed);
        Assert.Equal(0, vm.Standings.PulseSignal);
        rivals.Emit(new RivalsResult(1, 12, 11, 12, true));
        Assert.Equal("Your Global XP Rank is 1st of 12.", vm.Standings.Line);
        Assert.Equal("You gained 11 positions.", vm.Standings.DeltaLine);
        Assert.True(vm.Standings.Changed);
        Assert.Equal(1, vm.Standings.PulseSignal);
    }

    [Fact]
    public void Standings_AfterContinue_NoLongerUpdates()
    {
        var (vm, continued, rivals) = Create();
        vm.ContinueCommand.Execute(null);
        Assert.True(continued[0]);
        // Unity unhooks its rivals handler in Hide(); leaving the page must
        // not keep a dead receipt alive on the service event.
        rivals.Emit(new RivalsResult(2, 9, 3, 5, false));
        Assert.Equal("Your Global XP Rank is —/—.", vm.Standings.Line);
    }

    [Fact]
    public void Rarity_NoBoard_HidesTheGlobalKmLine()
    {
        var (vm, _, _) = Create(carKey: "ks_test");
        Assert.False(vm.HasRarityLine);
        Assert.Equal(string.Empty, vm.RarityLine);
    }

    [Fact]
    public void Rarity_CommonHalf_ReadsMostCommonlyUsed()
    {
        var rarity = new FakeCarRarity { CarCount = 10 };
        rarity.KmByKey["ks_test"] = 1234;
        rarity.RarityByKey["ks_test"] = 2;
        var (vm, _, _) = Create(rarity: rarity, carKey: "ks_test");
        Assert.True(vm.HasRarityLine);
        Assert.Equal(
            "Your car has been driven 1234 km by all players combined and is the 2nd most commonly used car in the game.",
            vm.RarityLine);
    }

    [Fact]
    public void Rarity_RareHalf_CountsFromTheBottom()
    {
        var rarity = new FakeCarRarity { CarCount = 10 };
        rarity.KmByKey["ks_test"] = 7;
        rarity.RarityByKey["ks_test"] = 9;
        var (vm, _, _) = Create(rarity: rarity, carKey: "ks_test");
        Assert.True(vm.HasRarityLine);
        Assert.Equal(
            "Your car has been driven 7 km by all players combined and is the 2nd least driven car in the game.",
            vm.RarityLine);
    }

    [Fact]
    public void Rarity_MostCommonCar_UsesTheUnnumberedSentence()
    {
        var rarity = new FakeCarRarity { CarCount = 10 };
        rarity.KmByKey["ks_test"] = 9000;
        rarity.RarityByKey["ks_test"] = 1;
        var (vm, _, _) = Create(rarity: rarity, carKey: "ks_test");
        Assert.Equal(
            "Your car has been driven 9000 km by all players combined and is the most commonly used car in the game.",
            vm.RarityLine);
    }

    [Fact]
    public void Rarity_CarMissingFromTheBoard_ReadsLeastDriven()
    {
        // Unity's unknown-car rank is carCount + 1; the bottom-half math
        // would say "0th least driven" without the clamp.
        var rarity = new FakeCarRarity { CarCount = 10 };
        rarity.KmByKey["ks_test"] = 0;
        rarity.RarityByKey["ks_test"] = 11;
        var (vm, _, _) = Create(rarity: rarity, carKey: "ks_test");
        Assert.Equal(
            "Your car has been driven 0 km by all players combined and is the least driven car in the game.",
            vm.RarityLine);
    }
}
