using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The new-business explanation page (TOZONES §4.5): the resx copy per POI
/// family, the order the pages open in after an expansion, and the one verb
/// that runs the continuation. Copy is asserted through the resx, so a
/// translator's edit is what the page shows.
/// </summary>
public sealed class PoiIntroScreenViewModelTests
{
    // The copy resolves through the current UI culture: pin it to en-US like
    // every other page suite, or a Dutch machine would render the satellites.
    public PoiIntroScreenViewModelTests() =>
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture =
            new CultureInfo("en-US");

    private static PointOfInterest Poi(string type) => new(
        $"{type}_0", "A Door",
        new AddressLocation("Street 6", 1, HouseSide.Right), type);

    [Fact]
    public void Copy_ComesFromTheResx_WithThePluralFamilyTitle()
    {
        PoiIntro intro = Assert.IsType<PoiIntro>(PoiIntroCopy.For(PoiTypes.CarWash));

        Assert.Equal("Car Washes", intro.Title);
        Assert.Equal(Strings.Get("PoiIntroCarWashBody"), intro.Body);
        Assert.StartsWith("Transporting customers and cargo", intro.Body, StringComparison.Ordinal);
        // The resx keeps the copy's own paragraph break, so the page reads in
        // two blocks instead of one run-on sentence.
        Assert.Contains("\n\n", intro.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ForAdded_OpensOnePagePerFamily_InBankOrder()
    {
        IReadOnlyList<PoiIntro> intros = PoiIntroCopy.ForAdded(
            [Poi(PoiTypes.Bank), Poi(PoiTypes.Bank), Poi(PoiTypes.CarWash)]);

        Assert.Equal(
            ["Banks", "Car Washes"], intros.Select(intro => intro.Title));
    }

    [Fact]
    public void FamiliesWithoutAuthoredCopy_HaveNoPage()
    {
        // Store doors and the pizzeria have no explanation page yet: the page
        // is skipped rather than printing a resx key.
        Assert.Null(PoiIntroCopy.For(PoiTypes.GroceryStore));
        Assert.Null(PoiIntroCopy.For(PoiTypes.CoffeeShop));
        Assert.Null(PoiIntroCopy.For(PoiTypes.PizzaPlace));
        Assert.Empty(PoiIntroCopy.ForAdded([Poi(PoiTypes.GroceryStore)]));
    }

    [Fact]
    public void Continue_RunsTheContinuation()
    {
        int continued = 0;
        var vm = new PoiIntroScreenViewModel(
            Assert.IsType<PoiIntro>(PoiIntroCopy.For(PoiTypes.Bank)),
            () => continued++,
            new VoiceBeat(new FakeClock()));

        vm.ContinueCommand.Execute(null);

        Assert.Equal(1, continued);
        Assert.False(vm.ContinueFlash);
    }

    [Fact]
    public void VoiceTap_AnswersItsLabel_ThenRunsAfterTheBeat()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        int continued = 0;
        var vm = new PoiIntroScreenViewModel(
            Assert.IsType<PoiIntro>(PoiIntroCopy.For(PoiTypes.Bank)),
            () => continued++, beat);

        Assert.True(vm.VoiceActions.TryGetValue(
            Strings.Get("PoiIntroContinue"), out Action? tap));
        tap!();
        Assert.True(vm.ContinueFlash);
        Assert.Equal(0, continued);

        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();

        Assert.Equal(1, continued);
    }

    [Fact]
    public void Click_DropsAPendingVoiceTap_SoTheContinuationRunsOnce()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        int continued = 0;
        var vm = new PoiIntroScreenViewModel(
            Assert.IsType<PoiIntro>(PoiIntroCopy.For(PoiTypes.Bank)),
            () => continued++, beat);

        vm.VoiceActions[Strings.Get("PoiIntroContinue")]();
        vm.ContinueCommand.Execute(null);
        clock.Advance(VoiceBeat.Duration + TimeSpan.FromMilliseconds(1));
        beat.Poll();

        Assert.Equal(1, continued);
    }

    [Fact]
    public void Page_KeepsThePanelOpen_AndIsAVoiceScreen()
    {
        var vm = new PoiIntroScreenViewModel(
            Assert.IsType<PoiIntro>(PoiIntroCopy.For(PoiTypes.Bank)),
            () => { },
            new VoiceBeat(new FakeClock()));

        Assert.IsAssignableFrom<IKeepPanelOpen>(vm);
        Assert.IsAssignableFrom<IVoiceScreen>(vm);
    }
}
