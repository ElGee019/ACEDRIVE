using AceDrive.Core;

namespace AceDrive.Core.Tests;

/// <summary>
/// The valuation's persisted half (PORTPLAN 49): the nine driver fields resume
/// a restart into the same showcase week, the Monday reset clears the week's
/// entry and venue, and the settled uid set unions without ever shrinking.
/// </summary>
public sealed class ValuationLedgerTests
{
    // The moment every test entry is made: the fake clock's Monday noon.
    private static readonly DateTime Entered = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_ResumesEverySavedField()
    {
        var saved = new DriverState(100.0, 50.0) with
        {
            ValuationVenueStreetId = "street-2",
            ValuationVenueHouseNumber = 7,
            ValuationDriving = true,
            ValuationEventsCompleted = 4,
            ValuationEntryWeek = 12,
            ValuationEntryCarKey = "ks_test",
            ValuationOwnScore = 123.5,
            ValuationEnteredAtUtc = Entered,
            ValuationSeenUids = ["rival-1", "rival-2"],
            LastValuationResetWeek = 12,
        };

        var ledger = new ValuationLedger(saved);

        Assert.Equal("street-2", ledger.VenueStreetId);
        Assert.Equal(7, ledger.VenueHouseNumber);
        Assert.True(ledger.Driving);
        Assert.Equal(4, ledger.EventsCompleted);
        Assert.Equal(12, ledger.EntryWeek);
        Assert.Equal("ks_test", ledger.EntryCarKey);
        Assert.Equal(123.5, ledger.OwnScore);
        Assert.Equal(Entered, ledger.EnteredAtUtc);
        Assert.Equal(["rival-1", "rival-2"], ledger.SeenUids);
        Assert.Equal(12, ledger.LastResetWeek);
    }

    [Fact]
    public void Constructor_OnAFreshDriver_StartsEmpty()
    {
        var ledger = new ValuationLedger(DriverState.Fresh);

        Assert.False(ledger.HasVenue);
        Assert.False(ledger.Driving);
        Assert.Equal(0, ledger.EventsCompleted);
        Assert.Equal(ValuationLedger.NoWeek, ledger.EntryWeek);
        Assert.Equal(ValuationLedger.NoWeek, ledger.LastResetWeek);
        Assert.False(ledger.HasEntry(3));
        Assert.Empty(ledger.SeenUids);
    }

    [Fact]
    public void HasEntry_OnlyAnswersForTheStoredWeek()
    {
        var ledger = new ValuationLedger();
        ledger.MarkEntry("ks_test", 42.0, week: 5, Entered);

        Assert.True(ledger.HasEntry(5));
        Assert.False(ledger.HasEntry(6));
        Assert.Equal("ks_test", ledger.EntryCarKey);
        Assert.Equal(42.0, ledger.OwnScore);
    }

    [Fact]
    public void MarkEntry_OpensTheTwoDayWindow()
    {
        var ledger = new ValuationLedger();
        ledger.MarkEntry("ks_test", 42.0, week: 5, Entered);

        Assert.Equal(Entered, ledger.EnteredAtUtc);
        Assert.Equal(Entered.AddHours(48), ledger.ExpiresAtUtc);
        Assert.False(ledger.IsExpired(Entered.AddHours(47.9)));
        Assert.True(ledger.IsExpired(Entered.AddHours(48)));
    }

    [Fact]
    public void Constructor_ResumesTheEntryStampSoTheWindowKeepsRunning()
    {
        var saved = new DriverState(0.0, 0.0) with
        {
            ValuationEntryWeek = 5,
            ValuationEntryCarKey = "ks_test",
            ValuationOwnScore = 42.0,
            ValuationEnteredAtUtc = Entered,
            LastValuationResetWeek = 5,
        };

        var ledger = new ValuationLedger(saved);

        Assert.Equal(Entered, ledger.EnteredAtUtc);
        Assert.True(ledger.IsExpired(Entered.AddHours(48)));
    }

    [Fact]
    public void EntryWithoutAStamp_IsAlreadyExpired()
    {
        // A save written before the stamp existed: there is no moment to count
        // the two-day window from, so the cap cannot be shown to hold.
        var ledger = new ValuationLedger(new DriverState(0.0, 0.0) with
        {
            ValuationEntryWeek = 5,
            ValuationEntryCarKey = "ks_test",
            ValuationOwnScore = 42.0,
        });

        Assert.Equal(DateTime.MinValue, ledger.ExpiresAtUtc);
        Assert.True(ledger.IsExpired(Entered));
    }

    [Fact]
    public void ClearEntry_DropsTheStampWithTheEntry()
    {
        var ledger = new ValuationLedger();
        ledger.MarkEntry("ks_test", 42.0, week: 5, Entered);

        ledger.ClearEntry();

        Assert.Null(ledger.EnteredAtUtc);
        Assert.Null(ledger.ExpiresAtUtc);
        Assert.False(ledger.IsExpired(Entered.AddDays(9)));
    }

    [Fact]
    public void RecordSeen_UnionsWithoutDuplicates()
    {
        var ledger = new ValuationLedger(
            new DriverState(0.0, 0.0) with { ValuationSeenUids = ["a", "a", "b"] });

        Assert.Equal(["a", "b"], ledger.SeenUids);

        ledger.RecordSeen(["b", "c", "", null!]);

        Assert.Equal(["a", "b", "c"], ledger.SeenUids);
    }

    [Fact]
    public void MarkReset_ClearsTheWeekAndStampsTheNewOne()
    {
        var ledger = new ValuationLedger();
        ledger.SetVenue(new ValuationVenue("street-1", "Street 1", 3));
        ledger.BeginDrive();
        ledger.MarkEntry("ks_test", 42.0, week: 5, Entered);
        ledger.RecordSeen(["rival-1"]);

        ledger.MarkReset(week: 6);

        Assert.False(ledger.HasVenue);
        Assert.False(ledger.Driving);
        Assert.False(ledger.HasEntry(5));
        Assert.False(ledger.HasEntry(6));
        Assert.Null(ledger.EntryCarKey);
        Assert.Empty(ledger.SeenUids);
        Assert.Equal(6, ledger.LastResetWeek);
    }

    [Fact]
    public void ClearEntry_KeepsTheSettledSetForTheSilentReseed()
    {
        // A vanished own doc must not un-settle the list the flow just read:
        // the Tuesday wipe and the live reseed both rely on that.
        var ledger = new ValuationLedger();
        ledger.MarkEntry("ks_test", 42.0, week: 5, Entered);
        ledger.RecordSeen(["rival-1"]);

        ledger.ClearEntry();

        Assert.False(ledger.HasEntry(5));
        Assert.Null(ledger.EntryCarKey);
        Assert.Equal(["rival-1"], ledger.SeenUids);
    }

    [Fact]
    public void CompleteEvent_ConsumesTheVenueAndCountsTheEvent()
    {
        var ledger = new ValuationLedger();
        ledger.SetVenue(new ValuationVenue("street-1", "Street 1", 3));
        ledger.BeginDrive();

        ledger.CompleteEvent();

        Assert.False(ledger.HasVenue);
        Assert.False(ledger.Driving);
        Assert.Equal(0, ledger.VenueHouseNumber);
        Assert.Equal(1, ledger.EventsCompleted);
    }

    [Fact]
    public void Snapshot_RoundTripsThroughTheConstructor()
    {
        var ledger = new ValuationLedger();
        ledger.SetVenue(new ValuationVenue("street-2", "Street 2", 9));
        ledger.BeginDrive();
        ledger.MarkEntry("ks_test", 77.25, week: 4, Entered);
        ledger.RecordSeen(["rival-1", "rival-2"]);

        var saved = new DriverState(0.0, 0.0) with
        {
            ValuationVenueStreetId = ledger.VenueStreetId,
            ValuationVenueHouseNumber = ledger.VenueHouseNumber,
            ValuationDriving = ledger.Driving,
            ValuationEventsCompleted = ledger.EventsCompleted,
            ValuationEntryWeek = ledger.EntryWeek,
            ValuationEntryCarKey = ledger.EntryCarKey,
            ValuationOwnScore = ledger.OwnScore,
            ValuationSeenUids = [.. ledger.SeenUids],
            LastValuationResetWeek = ledger.LastResetWeek,
            ValuationEnteredAtUtc = ledger.EnteredAtUtc,
        };
        var resumed = new ValuationLedger(saved);

        Assert.Equal(ledger.VenueStreetId, resumed.VenueStreetId);
        Assert.Equal(ledger.VenueHouseNumber, resumed.VenueHouseNumber);
        Assert.True(resumed.Driving);
        Assert.True(resumed.HasEntry(4));
        Assert.Equal(77.25, resumed.OwnScore);
        Assert.Equal(Entered, resumed.EnteredAtUtc);
        Assert.Equal(ledger.SeenUids, resumed.SeenUids);
    }
}
