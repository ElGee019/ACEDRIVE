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
        PlayerPreferences? preferences = null, VoiceBeat? beat = null,
        FakeClock? clock = null)
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        return new HudViewModel(
            AddressMap.Default,
            preferences ?? PlayerPreferences.Default,
            beat ?? new VoiceBeat(new FakeClock()),
            clock ?? new FakeClock(),
            TaxiRules.Default);
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
    public void ShowTaskText_RaisesTheSurfaceRequest()
    {
        var vm = Create();
        int requests = 0;
        vm.TaskTextRequested += (_, _) => requests++;

        vm.ShowTaskText();

        Assert.Equal(1, requests);
    }

    [Fact]
    public void Request_ReplacesTheTaskTextThenClears()
    {
        // The customer request rides the companion (PORTPLAN 11): the view
        // hides the normal task text and paints this VM while it is set.
        var vm = Create();
        Assert.True(vm.HasNoRequest);
        Assert.False(vm.HasRequest);
        Assert.Null(vm.Request);

        var request = new CustomerRequestScreenViewModel(
            new CustomerRequest(CustomerRequestKind.Passive, "Could you avoid the potholes?"),
            "Max Weber", 14, new FakeClock(), new VoiceBeat(new FakeClock()), _ => { });

        vm.ShowRequest(request);

        Assert.True(vm.HasRequest);
        Assert.False(vm.HasNoRequest);
        Assert.Same(request, vm.Request);
        Assert.Same(request, ((ITelemetryHud)vm).RequestScreen);

        vm.ClearRequest();

        Assert.False(vm.HasRequest);
        Assert.True(vm.HasNoRequest);
        Assert.Null(vm.Request);
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
    public void PersonDestination_CarriesTheStoryHint()
    {
        var vm = Create();

        vm.SetDestination(Friend(), new HudDestinationCopy("HudHintJustin"));

        Assert.True(vm.HasBanner);
        Assert.Equal(
            "Drive to Justin's Place, Street 1 No. 17 in Brands. " +
            "Your childhood friend still lives in the street where you grew up.",
            vm.BannerText);
    }

    [Fact]
    public void AnnabellaDestination_LeadsWithThePancakeLine()
    {
        var vm = Create();

        vm.SetDestination(Annabella(), new HudDestinationCopy("HudHintAnnabella"));

        Assert.Equal(
            "Friday has always been Pancake Day at grandma's! Let's bring that tradition back. " +
            "Drive to Annabella's Place, Street 1 No. 17 in Brands and pick up Annabella like old times.",
            vm.BannerText);
    }

    [Fact]
    public void PersonDestination_ArrivalIsAPlainArrivalWithAPickUpAction()
    {
        var vm = Create();
        vm.SetDestination(Friend());

        // 0.25 resolves to Street 1 No. 17, the friend's own house.
        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);

        Assert.True(vm.AtDestination);
        Assert.True(vm.ShowEnterButton);
        Assert.Equal("You have arrived at Justin's Place.", vm.DestinationText);
        Assert.Equal("PICK UP", vm.EnterText);
    }

    [Fact]
    public void HomeDestination_CarriesTheStoryCopyAndThePancakeAction()
    {
        var vm = Create();
        var home = new PointOfInterest(
            StoryHome.Definition.Id, StoryHome.Definition.Name,
            new AddressLocation("Street 1", 17, HouseSide.Right),
            StoryHome.Definition.Type);

        vm.SetDestination(home, new HudDestinationCopy(
            "HudHintHome", "HudArrivedHome", "HudPancakes"));

        Assert.Equal(
            "Head back to Grandma's House, Street 1 No. 17 in Brands. " +
            "She's probably waiting on that maple syrup by now.",
            vm.BannerText);
        Assert.Equal("PANCAKES!", vm.EnterText);

        // 0.25 resolves to Street 1 No. 17, the home door.
        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);

        Assert.True(vm.AtDestination);
        Assert.Equal("You have arrived at grandma's house.", vm.DestinationText);
        Assert.Equal("PANCAKES!", vm.EnterText);
    }

    [Fact]
    public void CommunityCentreDestination_ShowsTheLicenseCopyAndEnterAction()
    {
        var vm = Create();
        var centre = new PointOfInterest(
            StoryCommunityCentre.Definition.Id, StoryCommunityCentre.Definition.Name,
            new AddressLocation("Street 1", 17, HouseSide.Right),
            StoryCommunityCentre.Definition.Type);

        vm.SetDestination(centre, new HudDestinationCopy(
            "HudHintCommunityCentre", "HudArrivedCommunityCentre", "HudEnter"));

        Assert.Equal(
            "Drive to the Community Centre, Street 1 No. 17 in Brands. " +
            "They're handing out free taxi licenses today.",
            vm.BannerText);
        Assert.Equal("ENTER", vm.EnterText);

        // 0.25 resolves to Street 1 No. 17: the community centre's door.
        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);

        Assert.True(vm.AtDestination);
        Assert.True(vm.ShowEnterButton);
        Assert.Equal(
            "You have arrived at the Community Centre. " +
            "Enter to pick up your taxi license.",
            vm.DestinationText);
        Assert.Equal("ENTER", vm.EnterText);
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

    private static HudJobGuidance Job(
        string name = "Max", string street = "Street 1", int number = 17,
        DateTime? speedDeadline = null) =>
        new(name, new AddressLocation(street, number, HouseSide.Right),
            "HudJobPickupDrive", "HudJobPickupArrived", "HudJobPickupAction",
            25.0, speedDeadline);

    private static PointOfInterest Friend() =>
        new(StoryFriends.Justin.Id, StoryFriends.Justin.Name,
            new AddressLocation("Street 1", 17, HouseSide.Right),
            StoryFriends.Justin.Type);

    private static PointOfInterest Annabella() =>
        new(StoryFriends.Annabella.Id, StoryFriends.Annabella.Name,
            new AddressLocation("Street 1", 17, HouseSide.Right),
            StoryFriends.Annabella.Type);

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

    [Fact]
    public void JobGuidance_ReplacesTheStoryDestinationAndCountsDown()
    {
        var vm = Create();
        vm.SetDestination(Errand());
        vm.SetJobGuidance(Job());

        // Street 1 No. 17 sits at 480 m; 0.5 of the lap is 964.5 m.
        vm.Update(Sample(position: 0.5f, speedKmh: 50f), 10.0, gameRunning: true);

        Assert.True(vm.HasDestination);
        Assert.Equal("Pick up Max at Street 1 No. 17 in Brands", vm.BannerText);
        Assert.Equal("480 m", vm.LegDistanceText);
        Assert.Equal("PICK UP", vm.EnterText);
        Assert.False(vm.AtDestination);
    }

    [Fact]
    public void JobGuidance_ArrivesInsideTheRadiusAcrossTheStartLine()
    {
        var vm = Create();
        // Street 1 No. 1 is the line itself: 0.999 of the lap is 1.9 m short
        // the long way, 1,927 m the short way. The shorter way wins.
        vm.SetJobGuidance(Job(number: 1));

        vm.Update(Sample(position: 0.999f, speedKmh: 0f), 10.0, gameRunning: true);

        Assert.True(vm.AtDestination);
        Assert.True(vm.ShowEnterButton);
        Assert.Equal(
            "You are at the pickup point. Press PICK UP to let Max in.",
            vm.DestinationText);
        Assert.Equal("PICK UP", vm.EnterText);
    }

    [Fact]
    public void JobGuidance_OutsideTheRadiusStaysADrive()
    {
        var vm = Create();
        vm.SetJobGuidance(Job(number: 1));

        // 0.02 of the lap is ~38.6 m past the line: outside the 25 m radius.
        vm.Update(Sample(position: 0.02f, speedKmh: 0f), 10.0, gameRunning: true);

        Assert.False(vm.AtDestination);
        Assert.False(vm.ShowEnterButton);
        Assert.Equal("Pick up Max at Street 1 No. 1 in Brands", vm.DestinationText);
    }

    [Fact]
    public void JobGuidance_EnterRaisesTheJobEventOnly()
    {
        var vm = Create();
        vm.SetDestination(Errand());
        vm.SetJobGuidance(Job());
        vm.Update(Sample(position: 0.25f, speedKmh: 0f), 10.0, gameRunning: true);
        int story = 0;
        int job = 0;
        vm.EnterRequested += (_, _) => story++;
        vm.JobEnterRequested += (_, _) => job++;

        vm.EnterCommand.Execute(null);

        Assert.Equal(1, job);
        Assert.Equal(0, story);
    }

    [Fact]
    public void JobGuidance_PickUpAndDropOffEachAnswerTheirOwnWords()
    {
        // The companion's action button is the same control on both legs; the
        // spoken words follow the leg so the player says what the button says.
        var vm = Create();
        vm.SetJobGuidance(Job());

        Assert.Equal("PICK UP", vm.EnterText);
        Assert.Contains("pick up", vm.EnterKeywords);
        Assert.Contains("pickup", vm.EnterKeywords);
        Assert.DoesNotContain("drop off", vm.EnterKeywords);

        vm.SetJobGuidance(new HudJobGuidance(
            "Max", new AddressLocation("Street 1", 17, HouseSide.Right),
            "HudJobDropoffDrive", "HudJobDropoffArrived", "HudJobDropoffAction", 25.0));

        Assert.Equal("DROP OFF", vm.EnterText);
        Assert.Contains("drop off", vm.EnterKeywords);
        Assert.Contains("dropoff", vm.EnterKeywords);
        Assert.DoesNotContain("pick up", vm.EnterKeywords);
    }

    [Fact]
    public void PersonDestination_AnswersToPickUp()
    {
        var vm = Create();

        vm.SetDestination(Friend());

        Assert.Equal("PICK UP", vm.EnterText);
        Assert.Contains("pick up", vm.EnterKeywords);
    }

    [Fact]
    public void PlainEnterDestination_KeepsOnlyTheEnterWord()
    {
        var vm = Create();

        vm.SetDestination(Errand());

        Assert.Equal("ENTER", vm.EnterText);
        Assert.Equal("enter", Assert.Single(vm.EnterKeywords));
    }

    [Fact]
    public void TranslatedActionButton_StillAnswersTheEnglishWords()
    {
        // The Vosk model is English, so a translated label must not take the
        // spoken words with it: "AFZETTEN" still fires on "drop off".
        CultureInfo original = CultureInfo.CurrentUICulture;
        try
        {
            var vm = Create();
            CultureInfo.CurrentUICulture = new CultureInfo("nl-NL");
            vm.SetJobGuidance(new HudJobGuidance(
                "Max", new AddressLocation("Street 1", 17, HouseSide.Right),
                "HudJobDropoffDrive", "HudJobDropoffArrived",
                "HudJobDropoffAction", 25.0));

            Assert.Equal("AFZETTEN", vm.EnterText);
            Assert.Contains("drop off", vm.EnterKeywords);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void JobGuidance_ClearingRestoresTheStoryDestination()
    {
        // The job door owns the banner and the distance line while it is up;
        // clearing it hands both back to the intro leg underneath.
        var vm = Create();
        vm.SetDestination(new PointOfInterest("max-grocery", "Max's Grocery Store",
            new AddressLocation("Street 1", 1, HouseSide.Right)));
        vm.SetJobGuidance(Job());

        vm.Update(Sample(position: 0.5f, speedKmh: 50f), 10.0, gameRunning: true);
        Assert.Equal("480 m", vm.LegDistanceText); // the job's house

        vm.SetJobGuidance(null);

        Assert.True(vm.HasDestination);
        Assert.Equal("Drive to Max's Grocery Store, Street 1 No. 1 in Brands", vm.DestinationText);
        Assert.Equal("960 m", vm.LegDistanceText); // the story door again
    }

    [Fact]
    public void IntroDestination_ShowsTheDistanceToTheDoor()
    {
        // The intro drives carry the same distance line a taxi leg does: the
        // errand, the friends, home and the centre all pin a story
        // destination, so the driver reads how far the door is.
        var vm = Create();
        vm.SetDestination(Errand());

        vm.Update(Sample(position: 0.5f, speedKmh: 50f), 10.0, gameRunning: true);

        Assert.Equal("480 m", vm.LegDistanceText);
        Assert.Equal("480 m", vm.HudLineText);
        Assert.True(vm.HasHudLine);
    }

    [Fact]
    public void IntroDestination_PinningANewLegRepaintsTheDistanceAtOnce()
    {
        // The tour pins the next house while the car is still at the last
        // one: the line must answer from the last live sample instead of
        // waiting for the next poll.
        var vm = Create();
        vm.SetDestination(Errand());
        vm.Update(Sample(position: 0.5f, speedKmh: 0f), 10.0, gameRunning: true);
        Assert.Equal("480 m", vm.LegDistanceText);

        vm.SetDestination(new PointOfInterest("max-grocery", "Max's Grocery Store",
            new AddressLocation("Street 1", 1, HouseSide.Right)));

        Assert.Equal("960 m", vm.LegDistanceText);
    }

    [Fact]
    public void IntroDestination_ClearingTheLegClearsTheDistance()
    {
        var vm = Create();
        vm.SetDestination(Errand());
        vm.Update(Sample(position: 0.5f, speedKmh: 50f), 10.0, gameRunning: true);

        vm.SetDestination(null);

        Assert.Equal(string.Empty, vm.LegDistanceText);
        Assert.False(vm.HasHudLine);
    }

    [Fact]
    public void JobGuidance_WrongCarWarningStillWins()
    {
        var vm = Create();
        vm.SetJobGuidance(Job());
        vm.Update(Sample(position: 0.5f, speedKmh: 0f), 10.0, gameRunning: true, wrongCar: true);

        Assert.Equal(Strings.Get("HudWrongCar"), vm.BannerText);
        Assert.True(vm.HasBanner);
    }

    [Fact]
    public void SpeedChallenge_CountsDownThenSaysLate()
    {
        var clock = new FakeClock();
        var vm = Create(clock: clock);
        vm.SetJobGuidance(Job(
            speedDeadline: clock.UtcNow.AddSeconds(90)));

        Assert.True(vm.HasSpeedChallenge);
        Assert.Equal("Speed challenge: 1:30", vm.SpeedChallengeText);
        Assert.False(vm.SpeedChallengeLate);

        clock.Advance(TimeSpan.FromSeconds(30));
        vm.Update(Sample(position: 0.5f, speedKmh: 40f), 10.0, gameRunning: true);
        Assert.Equal("Speed challenge: 1:00", vm.SpeedChallengeText);

        clock.Advance(TimeSpan.FromSeconds(61));
        vm.Update(Sample(position: 0.5f, speedKmh: 40f), 10.0, gameRunning: true);
        Assert.Equal(Strings.Get("HudSpeedChallengeLate"), vm.SpeedChallengeText);
        Assert.True(vm.SpeedChallengeLate);

        vm.SetJobGuidance(null);
        Assert.False(vm.HasSpeedChallenge);
        Assert.Equal(string.Empty, vm.SpeedChallengeText);
        Assert.False(vm.SpeedChallengeLate);
    }

    [Fact]
    public void Notice_WinsTheLineThenExpiresOnTheClock()
    {
        var clock = new FakeClock();
        var vm = Create(clock: clock);
        vm.SetJobGuidance(Job());
        vm.Update(Sample(position: 0.5f, speedKmh: 50f), 10.0, gameRunning: true);
        Assert.Equal("480 m", vm.HudLineText);

        vm.ShowNotice("Speeding fine -€50.00", TimeSpan.FromSeconds(3));
        Assert.True(vm.HasNotice);
        Assert.Equal("Speeding fine -€50.00", vm.HudLineText);

        clock.Advance(TimeSpan.FromSeconds(2.9));
        vm.Update(Sample(position: 0.5f, speedKmh: 50f), 10.0, gameRunning: true);
        Assert.True(vm.HasNotice);

        clock.Advance(TimeSpan.FromSeconds(0.2));
        vm.Update(Sample(position: 0.5f, speedKmh: 50f), 10.0, gameRunning: true);
        Assert.False(vm.HasNotice);
        Assert.Equal("480 m", vm.HudLineText);
    }

    [Fact]
    public void Notice_DrivingToTheNextDoor_KeepsTheNoticeUp()
    {
        // The distance line repaints under the notice; the notice keeps the
        // slot until its clock runs out.
        var clock = new FakeClock();
        var vm = Create(clock: clock);
        vm.SetJobGuidance(Job());
        vm.ShowNotice("Fine", TimeSpan.FromSeconds(3));

        vm.Update(Sample(position: 0.5f, speedKmh: 50f), 10.0, gameRunning: true);

        Assert.Equal("Fine", vm.HudLineText);
    }
}
