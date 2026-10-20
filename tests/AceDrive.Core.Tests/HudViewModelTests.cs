using System.Globalization;
using AceDrive.Core;
using AceDrive.Overlay.Localization;
using AceDrive.Overlay.Services;
using AceDrive.Overlay.ViewModels;

namespace AceDrive.Core.Tests;

/// <summary>The companion HUD: mileage, waiting states and the live
/// normalised lap position resolved through the placeholder address map.</summary>
public sealed class HudViewModelTests
{
    private static readonly TrackInfo BrandsIndy =
        new("brands_hatch", "indy", 1929.0);

    private static HudViewModel Create(
        PlayerPreferences? preferences = null, VoiceBeat? beat = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        return new HudViewModel(
            AddressMap.Default,
            preferences ?? PlayerPreferences.Default,
            beat ?? new VoiceBeat(new FakeClock()));
    }

    private static TelemetrySample Sample(
        float? position = 0.25f, TrackInfo? track = null, float speedKmh = 50f) =>
        new(1, 0f, 0f, 2, 1000, 0f, speedKmh, position, track ?? BrandsIndy);

    [Fact]
    public void BeforeAnyPoll_ShowsWaiting()
    {
        var vm = Create();
        Assert.Equal(Strings.Get("WaitingForEvo"), vm.HeaderText);
        Assert.Equal("no car", vm.MileageText);
        Assert.Equal("no track position", vm.AddressText);
        Assert.Equal("MILEAGE", vm.MileageLabel);
        Assert.Equal("CURRENT STREET", vm.AddressLabel);
        Assert.False(vm.HasDestination);
        Assert.Equal(string.Empty, vm.DestinationText);
        Assert.Equal(string.Empty, vm.EnterText);
        Assert.False(vm.ShowEnterButton);
        Assert.False(vm.WrongCar);
        Assert.False(vm.HasBanner);
        Assert.Equal(string.Empty, vm.BannerText);
        Assert.False(vm.IsRevealed);
    }

    [Fact]
    public void Reveal_TurnsTheCompanionOn()
    {
        var vm = Create();

        vm.Reveal();

        Assert.True(vm.IsRevealed);
    }

    [Fact]
    public void NoGame_ShowsWaitingAndKeepsTheCarMileage()
    {
        var vm = Create();
        vm.Update(null, 1234.0, gameRunning: false);

        Assert.Equal(Strings.Get("WaitingForEvo"), vm.HeaderText);
        Assert.Equal("001234.0", vm.MileageText);
        Assert.Equal("no track position", vm.AddressText);
    }

    [Fact]
    public void GameWithoutShm_ShowsNoShm()
    {
        var vm = Create();
        vm.Update(null, null, gameRunning: true);
        Assert.Equal(Strings.Get("EvoNoShm"), vm.HeaderText);
        Assert.Equal("no car", vm.MileageText);
    }

    [Fact]
    public void LiveSample_ShowsTrackMileageAndStreet()
    {
        var vm = Create();
        vm.Update(Sample(), odometerKm: 1234.6, gameRunning: true);

        Assert.Equal("brands_hatch · indy", vm.HeaderText);
        Assert.Equal("001234.6", vm.MileageText);
        Assert.Equal("Street 1 · No. 17, Brands", vm.AddressText);
    }

    [Fact]
    public void WrongCar_ShowsTheBannerAndKeepsTheLiveReadouts()
    {
        var vm = Create();
        vm.Update(Sample(), odometerKm: 1234.6, gameRunning: true, wrongCar: true);

        Assert.True(vm.WrongCar);
        Assert.True(vm.HasBanner);
        Assert.Equal(
            "Career mode disabled. Change into a car you own.",
            vm.BannerText);
        Assert.Equal("brands_hatch · indy", vm.HeaderText);
        Assert.Equal("Street 1 · No. 17, Brands", vm.AddressText);
        Assert.Equal("001234.6", vm.MileageText);
    }

    [Fact]
    public void WrongCar_OverridesTheErrandLineUntilTheRightCarReturns()
    {
        var vm = Create();
        vm.SetDestination(Errand());
        Assert.Equal("Drive to Max's Grocery Store, Street 1 No. 17 in Brands", vm.BannerText);

        vm.Update(Sample(), 10.0, gameRunning: true, wrongCar: true);
        Assert.Equal(Strings.Get("HudWrongCar"), vm.BannerText);

        vm.Update(Sample(), 10.0, gameRunning: true, wrongCar: false);
        Assert.False(vm.WrongCar);
        Assert.Equal("Drive to Max's Grocery Store, Street 1 No. 17 in Brands", vm.BannerText);
    }

    [Fact]
    public void WrongCar_WithoutALiveSample_StaysHidden()
    {
        var vm = Create();
        vm.Update(null, 10.0, gameRunning: true, wrongCar: true);

        Assert.False(vm.WrongCar);
        Assert.False(vm.HasBanner);
    }

    [Fact]
    public void Mileage_KeepsSixIntegerDigitsAndOneDecimal()
    {
        var vm = Create();
        vm.Update(null, 12.0, gameRunning: false);
        Assert.Equal("000012.0", vm.MileageText);

        vm.Update(null, 1234567.8, gameRunning: false);
        Assert.Equal("1234567.8", vm.MileageText);
    }

    [Fact]
    public void Miles_ConvertTheBankedKilometres()
    {
        var vm = Create(new PlayerPreferences(MileageUnit.Miles, UiLanguage.English));
        // 100 km / 1.609344 = 62.137... mi -> one decimal, floored.
        vm.Update(null, 100.0, gameRunning: false);
        Assert.Equal("000062.1", vm.MileageText);
    }

    [Fact]
    public void ApplyPreferences_SwitchesUnitAndRepaintsImmediately()
    {
        var vm = Create();
        vm.Update(null, 100.0, gameRunning: false);
        Assert.Equal("000100.0", vm.MileageText);

        vm.ApplyPreferences(new PlayerPreferences(MileageUnit.Miles, UiLanguage.English));

        Assert.Equal("000062.1", vm.MileageText);
    }

    [Fact]
    public void SetDestination_ShowsTheDriveToSentence()
    {
        var vm = Create();
        var poi = new PointOfInterest("max-grocery", "Max's Grocery Store",
            new AddressLocation("Street 2", 17, HouseSide.Right));

        vm.SetDestination(poi);

        Assert.True(vm.HasDestination);
        Assert.False(vm.ShowEnterButton);
        Assert.Equal("Drive to Max's Grocery Store, Street 2 No. 17 in Brands", vm.DestinationText);
        Assert.Equal("ENTER", vm.EnterText);
    }

    [Fact]
    public void SetDestination_Null_HidesTheLine()
    {
        var vm = Create();
        vm.SetDestination(new PointOfInterest("max-grocery", "Max's Grocery Store",
            new AddressLocation("Street 1", 1, HouseSide.Right)));

        vm.SetDestination(null);

        Assert.False(vm.HasDestination);
        Assert.Equal(string.Empty, vm.DestinationText);
        Assert.Equal(string.Empty, vm.EnterText);
        Assert.False(vm.ShowEnterButton);
    }

    [Fact]
    public void ApplyPreferences_KeepsTheDestinationPainted()
    {
        var vm = Create();
        vm.SetDestination(new PointOfInterest("max-grocery", "Max's Grocery Store",
            new AddressLocation("Street 1", 1, HouseSide.Right)));

        vm.ApplyPreferences(new PlayerPreferences(MileageUnit.Miles, UiLanguage.English));

        Assert.True(vm.HasDestination);
        Assert.Equal("Drive to Max's Grocery Store, Street 1 No. 1 in Brands", vm.DestinationText);
    }

    [Fact]
    public void ParkedOnTheDestination_ShowsTheArrivalLineAndEnterButton()
    {
        var vm = Create();
        vm.SetDestination(Errand());

        // 0.25 resolves to Street 1 No. 17: the errand's own house.
        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);

        Assert.True(vm.AtDestination);
        Assert.True(vm.ShowEnterButton);
        Assert.Equal(
            "You have arrived at Max's Grocery Store, enter to buy maple syrup for grandma.",
            vm.DestinationText);
        Assert.Equal("ENTER", vm.EnterText);
    }

    [Fact]
    public void MovingThroughTheDestination_KeepsTheDriveToSentence()
    {
        var vm = Create();
        vm.SetDestination(Errand());

        vm.Update(Sample(position: 0.25f, speedKmh: 25f), 10.0, gameRunning: true);

        Assert.False(vm.AtDestination);
        Assert.False(vm.ShowEnterButton);
        Assert.Equal("Drive to Max's Grocery Store, Street 1 No. 17 in Brands", vm.DestinationText);
    }

    [Fact]
    public void StoppingOnAnotherHouse_KeepsTheDriveToSentence()
    {
        var vm = Create();
        vm.SetDestination(Errand());

        vm.Update(Sample(position: 0.5f, speedKmh: 0f), 10.0, gameRunning: true);

        Assert.False(vm.AtDestination);
        Assert.Equal("Drive to Max's Grocery Store, Street 1 No. 17 in Brands", vm.DestinationText);
    }

    [Fact]
    public void DrivingOffTheDestination_RestoresTheDriveToSentence()
    {
        var vm = Create();
        vm.SetDestination(Errand());
        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);
        Assert.True(vm.AtDestination);

        vm.Update(Sample(position: 0.5f, speedKmh: 0f), 10.0, gameRunning: true);

        Assert.False(vm.AtDestination);
        Assert.False(vm.ShowEnterButton);
        Assert.Equal("Drive to Max's Grocery Store, Street 1 No. 17 in Brands", vm.DestinationText);
    }

    [Fact]
    public void EnterCommand_OnlyRaisesAtTheDestination()
    {
        var vm = Create();
        vm.SetDestination(Errand());
        bool raised = false;
        vm.EnterRequested += (_, _) => raised = true;

        vm.EnterCommand.Execute(null);
        Assert.False(raised);

        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);
        vm.EnterCommand.Execute(null);

        Assert.True(raised);
        Assert.True(vm.EnterFlash);
    }

    [Fact]
    public void EnterFlash_ClearsAfterTheSharedBeat()
    {
        var clock = new FakeClock();
        var vm = Create(beat: new VoiceBeat(clock));
        vm.SetDestination(Errand());
        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);

        vm.EnterCommand.Execute(null);
        Assert.True(vm.EnterFlash);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);

        Assert.False(vm.EnterFlash);
    }

    [Fact]
    public void VoiceEnter_FlashesThenActsAfterTheBeat()
    {
        var clock = new FakeClock();
        var beat = new VoiceBeat(clock);
        var vm = Create(beat: beat);
        vm.SetDestination(Errand());
        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);
        int raised = 0;
        vm.EnterRequested += (_, _) => raised++;

        vm.RequestEnter();

        Assert.True(vm.EnterFlash);
        Assert.Equal(0, raised);

        clock.Advance(TimeSpan.FromMilliseconds(500));
        beat.Poll();

        Assert.False(vm.EnterFlash);
        Assert.Equal(1, raised);
    }

    private static PointOfInterest Errand() =>
        new("max-grocery", "Max's Grocery Store",
            new AddressLocation("Street 1", 17, HouseSide.Right));

    [Fact]
    public void LiveWithoutPosition_ShowsTrackButNoAddress()
    {
        var vm = Create();
        vm.Update(Sample(position: null), odometerKm: 10.0, gameRunning: true);

        Assert.Equal("brands_hatch · indy", vm.HeaderText);
        Assert.Equal("no track position", vm.AddressText);
    }

    [Fact]
    public void TrackWithoutConfiguration_ShowsNameOnly()
    {
        var vm = Create();
        vm.Update(Sample(track: new TrackInfo("brands_hatch", "", 1929.0)), 10.0, true);
        Assert.Equal("brands_hatch", vm.HeaderText);
    }

    [Fact]
    public void Address_ChangesAtTheHalfLap()
    {
        var vm = Create();
        vm.Update(Sample(position: 0.49f), 10.0, true);
        Assert.Equal("Street 1 · No. 32, Brands", vm.AddressText);

        vm.Update(Sample(position: 0.5f), 10.0, true);
        Assert.Equal("Street 2 · No. 1, Brands", vm.AddressText);
    }

    [Fact]
    public void InvalidPosition_ShowsNoAddress()
    {
        var vm = Create();
        vm.Update(Sample(position: 1.2f), 10.0, true);
        Assert.Equal("no track position", vm.AddressText);
    }

    [Fact]
    public void LosingTheCar_ReplacesTheMileageWithNoCar()
    {
        var vm = Create();
        vm.Update(Sample(), 100.0, true);
        Assert.Equal("000100.0", vm.MileageText);

        vm.Update(null, null, true);
        Assert.Equal("no car", vm.MileageText);
    }

    [Fact]
    public void RepeatedIdenticalSnapshot_DoesNotRepaint()
    {
        var vm = Create();
        vm.Update(Sample(), 1234.0, true);

        int changes = 0;
        vm.PropertyChanged += (_, _) => changes++;
        // Different packet, same track object, same displayed tenth, same
        // street and number: the 10 Hz poll must not raise three events.
        vm.Update(Sample(), 1234.04, true);

        Assert.Equal(0, changes);
    }
}
