using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>
/// The missing-person page runs headless: Unity's intro sentence must name the
/// drawn elder the way the player sees them — no ride companion suffix, which a
/// resident's stored identity can still carry.
/// </summary>
public sealed class WhereIsWillyScreenTests
{
    [Fact]
    public void IntroLine_NamesTheElderWithoutTheCompanionSuffix()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        var resident = new Resident(
            new AddressLocation("Street 1", 1, HouseSide.Right),
            new ProceduralName("Klaus", "Berger", "and some friends"));

        var page = new WhereIsWillyScreenViewModel(
            resident, "Eifel", [],
            showNotHere: false, _ => { }, () => { },
            new RelayCommand(() => { }), new VoiceBeat(new FakeClock()));

        Assert.Contains("Old Klaus Berger has gone missing", page.IntroLine, StringComparison.Ordinal);
        Assert.DoesNotContain("and some friends", page.IntroLine, StringComparison.Ordinal);
        // The ask names where to look: the draw only ever hides the elder in a
        // coffee shop the operating zone covers.
        Assert.Contains(
            "coffeeshops inside your operating zone and bring Klaus home",
            page.IntroLine, StringComparison.Ordinal);
        // The one sentence keeps Unity's flow: select a shop to look for them.
        Assert.Contains("Select a coffee shop in Eifel", page.IntroLine, StringComparison.Ordinal);
        Assert.EndsWith("to find out whether Klaus is there.", page.IntroLine, StringComparison.Ordinal);
        // The hint is its own paragraph (WillyIntro's blank line): the ask and
        // the instruction never run together on the page.
        string intro = page.IntroLine.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("home?\n\nSelect a coffee shop in Eifel", intro, StringComparison.Ordinal);
    }
}
