using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Player zone/shift/country pushes land on the own doc/aggregate.</summary>
public sealed class FirestorePlayerTelemetryTests
{
    private static FirestorePlayerTelemetry Create(
        FakeFirestoreDatabase db, string? uid = "me", string country = "Germany")
    {
        var clock = new FakeClock();
        return new FirestorePlayerTelemetry(
            new FirestorePlayers(db, clock),
            new FirestoreCountries(db, clock),
            () => uid, new NullLogger(), CancellationToken.None);
    }

    [Fact]
    public async Task ZonePush_MergesTheZoneField()
    {
        var db = new FakeFirestoreDatabase();
        await Create(db).PushZoneKmAsync(12.5, "Ace");

        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_players/me", path);
        Assert.Equal(12.5, fields["longestZoneKm"]);
        Assert.Equal("Ace", fields["displayName"]);
    }

    [Fact]
    public async Task ShiftPush_MergesTheBestShift()
    {
        var db = new FakeFirestoreDatabase();
        var ticks = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc).Ticks;
        await Create(db).PushShiftEarningsAsync(500, ticks, "Ace");

        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_players/me", path);
        Assert.Equal(500.0, Assert.IsType<double>(fields["highestShiftEarnings"]));
        Assert.Equal("Ace", fields["displayName"]);
        Assert.IsType<string>(fields["highestShiftEarningsFirstEarnedAt"]);
    }

    [Fact]
    public async Task CountryPush_IncrementsTheCountryAggregate()
    {
        var db = new FakeFirestoreDatabase();
        await Create(db).IncrementCountryKmAsync("Germany", 8);

        var (path, fields, increments) = Assert.Single(db.Increments);
        Assert.Equal("acevo_countries/germany", path);
        Assert.Equal(8, increments["totalKmDriven"]);
        Assert.Equal("Germany", fields["displayName"]);
    }

    [Fact]
    public async Task NoUid_DropsEveryPush()
    {
        var db = new FakeFirestoreDatabase();
        FirestorePlayerTelemetry telemetry = Create(db, uid: null);

        await telemetry.PushZoneKmAsync(1, "Ace");
        await telemetry.PushShiftEarningsAsync(1, 1, "Ace");
        await telemetry.IncrementCountryKmAsync("Germany", 1);

        Assert.Empty(db.Sets);
        Assert.Empty(db.Increments);
    }
}
