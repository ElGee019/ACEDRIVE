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
            WhereIsWillyPhase.Searching, resident, resident.Address, "Eifel", [],
            showNotHere: false, _ => { }, () => { }, () => { },
            new RelayCommand(() => { }), new VoiceBeat(new FakeClock()));

        Assert.Contains("Old Klaus Berger has gone missing", page.IntroLine, StringComparison.Ordinal);
        Assert.DoesNotContain("and some friends", page.IntroLine, StringComparison.Ordinal);
        // The one sentence keeps Unity's flow: select a shop to look for them.
        Assert.Contains("Select a coffee shop in Eifel", page.IntroLine, StringComparison.Ordinal);
        Assert.EndsWith("to find out whether Klaus is there.", page.IntroLine, StringComparison.Ordinal);
    }
}
