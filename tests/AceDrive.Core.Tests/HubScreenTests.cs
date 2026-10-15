using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The OPEN NEW CAREER page runs headless against a fixture hub
/// catalog: tier row, career columns, the CredP spend and voice labels all
/// resolve from resx + the shared career (LSP in action).</summary>
public sealed class HubScreenTests
{
    private static readonly HubCatalog Hubs = new(
    [
        new HubTier(1, true,
        [
            new HubCareer("groceries", "GROCERIES",
                [new HubLevel("Grocers and Sons", 100), new HubLevel("The Food Hub", 150)]),
            new HubCareer("coffee_beans", "COFFEE BEANS", [new HubLevel("CoffeeUs", 100)]),
        ]),
        new HubTier(2, false, []),
    ]);

    private static readonly DeliveryStore Post =
        new("adenau-post", "Adenau Post", "Hahn", 12, 40, 2.50, "groceries");

    private static readonly DeliveryCatalog Board = new([Post], 0.7, 100);

    private static (
        DeliveryHubScreenViewModel Vm,
        DeliveryCareer Career,
        TestLog Log,
        bool[] Fired) // [0]=back
        Create(double credP = 100, FakeClock? clock = null, VoiceBeat? beat = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var career = new DeliveryCareer(Hubs, credP);
        var city = new CityEconomy(Board.Stores, CityRules.Default);
        var log = new TestLog();
        var fired = new bool[1];
        // Tests that assert the deferred voice BACK wait own the clock and
        // the beat pair; everyone else gets a throwaway.
        clock ??= new FakeClock();
        beat ??= new VoiceBeat(clock);
        var vm = new DeliveryHubScreenViewModel(
            career, Board, city, log, new RelayCommand(() => fired[0] = true), beat);
        return (vm, career, log, fired);
    }

    private static HubCell Cell(DeliveryHubScreenViewModel vm, int column, int level) =>
        vm.Columns[column].Cells[level];

    [Fact]
    public void Boot_ShowsTierRowAndCareerColumns()
    {
        var (vm, _, _, _) = Create();

        Assert.Equal(["TIER 1", "TIER 2"], vm.TierCells.Select(t => t.LabelLine).ToArray());
        Assert.True(vm.TierCells[0].IsActive);
        Assert.False(vm.TierCells[1].IsActive);

        Assert.Equal(["GROCERIES", "COFFEE BEANS"], vm.Columns.Select(c => c.TitleLine).ToArray());
        Assert.Equal(
            ["Grocers and Sons - 100CredP", "The Food Hub - 150CredP"],
            vm.Columns[0].Cells.Select(c => c.LabelLine).ToArray());
        // Price painted apart from the name (affordability cue); voice keeps
        // the whole-cell LabelLine.
        Assert.Equal("Grocers and Sons", Cell(vm, 0, 0).NameLine);
        Assert.Equal("- 100CredP", Cell(vm, 0, 0).PriceLine);
        // Unity FillColumn: the 150 hub shows its price muted, not buyable.
        Assert.False(Cell(vm, 0, 1).IsAffordable);
        Assert.False(Cell(vm, 0, 1).IsSelectable);

        Assert.Equal("Credibility Points: 100", vm.CredPLine);
        // No career open yet: the gated store is not on the map (Unity totals).
        Assert.Equal("City growth: ▬ 0 / hr", vm.CityGrowthLine);
        Assert.False(vm.OpenCareerCommand.CanExecute(null));
    }

    [Fact]
    public void SelectingAnAffordableHub_EnablesOpenCareer()
    {
        var (vm, _, _, _) = Create();
        vm.ChooseCareerCommand.Execute(Cell(vm, 0, 0));

        Assert.True(vm.OpenCareerCommand.CanExecute(null));
        Assert.True(Cell(vm, 0, 0).IsSelected);
    }

    [Fact]
    public void SelectingAnUnaffordableHub_IsIgnored()
    {
        var (vm, _, _, _) = Create();
        vm.ChooseCareerCommand.Execute(Cell(vm, 0, 1));

        Assert.False(vm.OpenCareerCommand.CanExecute(null));
        Assert.False(Cell(vm, 0, 1).IsSelected);
    }

    [Fact]
    public void TierSwitch_ClearsSelection_AndDisabledTierShowsComingSoon()
    {
        var (vm, _, _, _) = Create();
        vm.ChooseCareerCommand.Execute(Cell(vm, 0, 0));

        vm.ChooseTierCommand.Execute(vm.TierCells[1]);
        Assert.True(vm.TierCells[1].IsActive);
        Assert.Equal(["COMING SOON", "COMING SOON"], vm.Columns.Select(c => c.TitleLine).ToArray());
        Assert.All(vm.Columns, c => Assert.Empty(c.Cells));
        Assert.False(vm.OpenCareerCommand.CanExecute(null)); // Unity ClearSelection
    }

    [Fact]
    public void SingleCareerTier_GetsAComingSoonFiller()
    {
        // Unity's hand-built screen always draws two columns; the odd
        // column out stays a COMING SOON placeholder.
        var hubs = new HubCatalog([new HubTier(3, true,
            [new HubCareer("chinese_import", "CHINESE IMPORT", [new HubLevel("Yeng Hu Wholesale", 180)])])]);
        var career = new DeliveryCareer(hubs, 200);
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var vm = new DeliveryHubScreenViewModel(
            career, DeliveryCatalog.Default,
            new CityEconomy(DeliveryCatalog.Default.Stores, CityRules.Default),
            new TestLog(), new RelayCommand(() => { }),
            new VoiceBeat(new FakeClock()));

        Assert.Equal(["CHINESE IMPORT", "COMING SOON"], vm.Columns.Select(c => c.TitleLine).ToArray());
    }

    [Fact]
    public void OpenCareer_SpendsCredPAndMarksHubOwned()
    {
        var (vm, career, log, _) = Create();
        vm.ChooseCareerCommand.Execute(Cell(vm, 0, 0));
        vm.OpenCareerCommand.Execute(null);

        Assert.Equal(0, career.CredP);
        OwnedHub hub = Assert.Single(career.Owned);
        Assert.Equal("Grocers and Sons", hub.Name);
        Assert.Equal("groceries", hub.Category);

        Assert.Equal("Credibility Points: 0", vm.CredPLine);
        // Unity: owned cells drop the price and the selection; the connected
        // store now counts in the city growth headline.
        Assert.Equal("Grocers and Sons", Cell(vm, 0, 0).LabelLine);
        Assert.Equal("", Cell(vm, 0, 0).PriceLine); // owned: name only, no price
        Assert.True(Cell(vm, 0, 0).IsOwned);
        Assert.False(Cell(vm, 0, 0).IsSelected);
        Assert.Equal("CoffeeUs - 100CredP", Cell(vm, 1, 0).LabelLine);
        Assert.False(Cell(vm, 1, 0).IsAffordable);
        Assert.Equal("City growth: ▼ -1 / hr", vm.CityGrowthLine);
        Assert.False(vm.OpenCareerCommand.CanExecute(null));
        Assert.Contains("career opened: Grocers and Sons (groceries)", string.Join("|", log.Infos), StringComparison.Ordinal);
    }

    [Fact]
    public void OpenCareer_WithoutSelection_ChangesNothing()
    {
        var (vm, career, log, _) = Create();
        vm.OpenCareerCommand.Execute(null);

        Assert.Equal(100, career.CredP);
        Assert.Empty(career.Owned);
        Assert.Empty(log.Infos);
    }

    [Fact]
    public void BuyingOneCareer_LeavesTheOtherLocked()
    {
        var (vm, career, _, _) = Create(200);
        vm.ChooseCareerCommand.Execute(Cell(vm, 0, 1)); // The Food Hub, 150
        vm.OpenCareerCommand.Execute(null);

        // Unity numbers the level from its row, not from what it unlocks:
        // one owned groceries hub connects the whole category.
        Assert.Equal(50, career.CredP);
        OwnedHub hub = Assert.Single(career.Owned);
        Assert.Equal("The Food Hub", hub.Name);
        Assert.Equal(2, hub.Level);
        // The Food Hub now reads as just its name (Unity owned cell).
        Assert.Equal("The Food Hub", Cell(vm, 0, 1).LabelLine);
        // 50 CredP left: the other hubs stay muted and unbuyable until
        // deliveries earn more.
        Assert.False(Cell(vm, 1, 0).IsAffordable);
        Assert.False(Cell(vm, 0, 0).IsSelectable);
    }

    [Fact]
    public void TitleButton_PicksNextBuyableHub()
    {
        // The title button is the list's shortcut: "COFFEE BEANS" alone
        // prepares that career's next purchase, exactly like tapping its
        // first buyable cell.
        var (vm, _, _, _) = Create();
        Assert.True(vm.Columns[1].CanPick);
        vm.ChooseColumnCommand.Execute(vm.Columns[1]);

        Assert.True(Cell(vm, 1, 0).IsSelected);
        Assert.True(vm.OpenCareerCommand.CanExecute(null));
    }

    [Fact]
    public void TitleSelection_MovesBetweenCareerLists()
    {
        // One career picked, then the other tried: the first pick is
        // deselected and the highlight switches lists (no multi-select).
        var (vm, _, _, _) = Create();
        vm.ChooseColumnCommand.Execute(vm.Columns[0]);
        Assert.True(vm.Columns[0].IsSelected);
        Assert.False(vm.Columns[1].IsSelected);

        vm.ChooseColumnCommand.Execute(vm.Columns[1]);
        Assert.False(vm.Columns[0].IsSelected);
        Assert.False(Cell(vm, 0, 0).IsSelected);
        Assert.True(vm.Columns[1].IsSelected);
        Assert.True(Cell(vm, 1, 0).IsSelected);
        Assert.True(vm.OpenCareerCommand.CanExecute(null));
    }

    [Fact]
    public void TitleVoice_AnswersAndGoesQuietWhenNothingIsBuyable()
    {
        var (vm, career, _, _) = Create();
        Assert.True(vm.VoiceActions.ContainsKey("GROCERIES"));
        vm.VoiceActions["GROCERIES"].Invoke();
        Assert.True(Cell(vm, 0, 0).IsSelected);

        // Spend the last CredP: every hub left is owned or unaffordable,
        // so both title buttons leave the voice set with their rebuild.
        vm.VoiceActions["OPEN CAREER"].Invoke();
        Assert.Equal(0, career.CredP);
        Assert.True(Cell(vm, 0, 0).IsOwned);
        Assert.False(vm.Columns[0].CanPick);
        Assert.False(vm.VoiceActions.ContainsKey("GROCERIES"));
        Assert.False(vm.VoiceActions.ContainsKey("COFFEE BEANS"));
    }

    [Fact]
    public void HasSelectedCareer_TracksThePick_ForButtonVisibility()
    {
        // The view hides OPEN CAREER while this flag is false: it must
        // arrive with any pick (title or cell) and leave again after the
        // purchase clears the selection.
        var (vm, _, _, _) = Create();
        Assert.False(vm.HasSelectedCareer);

        vm.ChooseColumnCommand.Execute(vm.Columns[1]);
        Assert.True(vm.HasSelectedCareer);
        Assert.True(vm.OpenCareerCommand.CanExecute(null));

        vm.OpenCareerCommand.Execute(null);
        Assert.False(vm.HasSelectedCareer);
    }

    [Fact]
    public void Back_FiresInjectedCommand()
    {
        var (vm, _, _, fired) = Create();
        vm.BackCommand.Execute(null);
        Assert.True(fired[0]);
    }

    [Fact]
    public void Voice_EveryButtonAnswersToItsText()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var (vm, career, _, fired) = Create(clock: clock, beat: beat);
        Assert.True(vm.VoiceActions.ContainsKey("TIER 2"));
        Assert.True(vm.VoiceActions.ContainsKey("Grocers and Sons - 100CredP"));
        Assert.True(vm.VoiceActions.ContainsKey("OPEN CAREER"));
        Assert.True(vm.VoiceActions.ContainsKey("BACK"));

        vm.VoiceActions["Grocers and Sons - 100CredP"].Invoke();
        Assert.True(vm.OpenCareerCommand.CanExecute(null));
        vm.VoiceActions["OPEN CAREER"].Invoke();
        Assert.Equal(0, career.CredP);

        // Owned cells answer by their short label, like the drawn button.
        vm.VoiceActions["Grocers and Sons"].Invoke();
        Assert.Equal(0, career.CredP); // already owned: a pick changes nothing

        // Back navigates, so it waits out the shared beat; the picks above
        // stay on this page and act at once.
        vm.VoiceActions["BACK"].Invoke();
        Assert.False(fired[0]);
        DrainBeat(clock, beat);
        Assert.True(fired[0]);
    }

    [Fact]
    public void Voice_BackTap_GlowsFirst_NavigatesAtTheBeatEnd()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var (vm, _, _, fired) = Create(clock: clock, beat: beat);
        vm.VoiceActions["BACK"].Invoke();
        Assert.True(vm.BackFlash);
        Assert.False(fired[0]);
        DrainBeat(clock, beat);
        Assert.True(fired[0]);
        Assert.False(vm.BackFlash);
    }

    [Fact]
    public void Click_Back_NavigatesAtOnce_NoBeatWait()
    {
        var (vm, _, _, fired) = Create();
        vm.BackCommand.Execute(null);
        Assert.True(fired[0]);
    }

    // What MainViewModel.Poll does at 10 Hz: wall time passes, the beat drains.
    private static void DrainBeat(FakeClock clock, VoiceBeat beat)
    {
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();
    }
}
