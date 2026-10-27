using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The market-valuation page runs headless: the car picker and its stat rows,
/// the plain ENTER label the mic can hear, the voice taps with their shared-beat
/// glow, and the comparison phase that swaps the bottom verb to CONTINUE.
/// </summary>
public sealed class ValuationScreenTests
{
    private static readonly CarSpec TestCar = new(
        "ks_valuation_car", 2020, "Test", "One", Passengers: 2, Class: 5,
        Price: 20000.0, Horsepower: 150, EngineCc: 1600, TopSpeedKmh: 200);

    private sealed record Harness(
        ValuationScreenViewModel Vm, FakeClock Clock, VoiceBeat Beat,
        List<string> Entered, List<double> Continued, bool[] Closed);

    private static Harness Create(double ranking = OwnedCar.DefaultLeaderboardRanking)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var entered = new List<string>();
        var continued = new List<double>();
        var closed = new bool[1];
        var garage = new Garage([TestCar]);
        garage.Add(TestCar.Key, week: 1).SetLeaderboardRanking(ranking);
        var catalog = new Dictionary<string, CarSpec>(StringComparer.Ordinal)
        {
            [TestCar.Key] = TestCar,
        };

        var vm = new ValuationScreenViewModel(
            garage, catalog, "Street 1 9",
            key => entered.Add(key),
            xp => continued.Add(xp),
            new RelayCommand(() => closed[0] = true),
            beat);
        return new Harness(vm, clock, beat, entered, continued, closed);
    }

    [Fact]
    public void EntryPhase_LabelsEnterPlainlyAndRegistersItsVoiceTaps()
    {
        var h = Create();

        // The label is the voice key: "ENTER", never "ENTER WITH {car}".
        Assert.Equal("ENTER", h.Vm.EnterLabel);
        Assert.True(h.Vm.ShowBack);
        Assert.True(h.Vm.VoiceActions.ContainsKey("ENTER"));
        Assert.True(h.Vm.VoiceActions.ContainsKey("BACK"));
        // The picked car still answers by its title, so voice can change it.
        Assert.Contains(h.Vm.Cars[0].Title, h.Vm.VoiceActions.Keys);
        // Price, engine, power, top speed, 0-60, class and the ranking.
        Assert.Equal(7, h.Vm.Stats.Count);
    }

    [Fact]
    public void Stats_ReadInCapsAndExposeTheCarsOwnRanking()
    {
        // A car ranked halfway up its model's field prints 0.5 — the 0..1 value
        // the score curve reads, not a percentage — so the row follows the
        // owned car rather than the catalogue row or a placeholder.
        var h = Create(ranking: 0.5);

        // The page spells every caption upper case, so the terms of the score
        // read as one column whatever the translation's casing rules are.
        Assert.All(h.Vm.Stats, row => Assert.Equal(row.Label.ToUpperInvariant(), row.Label));
        GarageStatRow ranking = h.Vm.Stats[^1];
        Assert.Equal(Strings.Get("ValuationLeaderboardLabel"), ranking.Label);
        Assert.Equal("0.5", ranking.Value);
        // The starter placeholder is the top percentile and reads 1, not 100%.
        Assert.Equal("1", Create().Vm.Stats[^1].Value);
    }

    [Fact]
    public void IntroLine_KeepsTheTermsParagraphBelowTheAsk()
    {
        var h = Create();

        // The blank line between the ask and the terms is part of the copy; the
        // view only lays the two paragraphs out, so the break must survive the
        // resx (which stores the file's CRLFs).
        string intro = h.Vm.IntroLine.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("\n\n", intro, StringComparison.Ordinal);
    }

    [Fact]
    public void ClickEnter_EntersAtOnce()
    {
        var h = Create();

        h.Vm.EnterCommand.Execute(null);

        Assert.Equal(TestCar.Key, Assert.Single(h.Entered));
        Assert.False(h.Vm.EnterFlash);
    }

    [Fact]
    public void VoiceEnter_GlowsThenEntersAfterTheBeat()
    {
        var h = Create();

        h.Vm.VoiceActions["ENTER"]();

        Assert.True(h.Vm.EnterFlash);
        Assert.Empty(h.Entered);

        h.Clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        h.Beat.Poll();

        Assert.Equal(TestCar.Key, Assert.Single(h.Entered));
        Assert.False(h.Vm.EnterFlash);
    }

    [Fact]
    public void ComparisonPhase_SwapsTheBottomVerbToContinue()
    {
        var h = Create();

        h.Vm.ShowWaiting("Ace", TestCar.Key, 12.5);
        h.Vm.ShowCompetitor(
            new ValuationEntry("rival", "Rival", TestCar.Key, 1, 0.0, 1.0),
            new ValuationVerdict(Won: true, Gap: 11.5, Xp: 0.25));

        // ENTER and BACK leave the page; CONTINUE takes the bottom slot.
        Assert.False(h.Vm.ShowBack);
        Assert.True(h.Vm.HasCompetitor);
        Assert.False(h.Vm.VoiceActions.ContainsKey("ENTER"));
        Assert.True(h.Vm.VoiceActions.ContainsKey("CONTINUE"));

        h.Vm.VoiceActions["CONTINUE"]();

        Assert.True(h.Vm.ContinueFlash);
        Assert.Empty(h.Continued);

        h.Clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        h.Beat.Poll();

        Assert.Equal(0.25, Assert.Single(h.Continued));
        Assert.False(h.Vm.ContinueFlash);
    }

    [Fact]
    public void VoiceBack_GlowsThenClosesAfterTheBeat()
    {
        var h = Create();

        h.Vm.VoiceActions["BACK"]();

        Assert.True(h.Vm.BackFlash);
        Assert.False(h.Closed[0]);

        h.Clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        h.Beat.Poll();

        Assert.True(h.Closed[0]);
        Assert.False(h.Vm.BackFlash);
    }
}
