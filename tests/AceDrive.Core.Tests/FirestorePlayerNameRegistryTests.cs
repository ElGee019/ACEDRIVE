using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Global name reservation: create-only claim, own reclaim, taken.</summary>
public sealed class FirestorePlayerNameRegistryTests
{
    private static FirestorePlayerNameRegistry Registry(FakeFirestoreDatabase db) =>
        new(db, _ => Task.FromResult<string?>("uid-1"),
            new FakeClock(), new NullLogger());

    [Fact]
    public async Task Claim_NewName_CreatesTheDoc()
    {
        var db = new FakeFirestoreDatabase();

        Assert.Equal(NameClaimResult.Claimed, await Registry(db).ClaimAsync("Ace Driver"));

        var (path, fields) = Assert.Single(db.Sets);
        Assert.Equal("acevo_playerNames/ace-driver", path);
        Assert.Equal("uid-1", fields["ownerUid"]);
        Assert.Equal("Ace Driver", fields["displayName"]);
    }

    [Fact]
    public async Task Claim_TakenByAnotherUid_ReportsTaken()
    {
        var db = new FakeFirestoreDatabase { NextCreateResult = FirestoreCreateResult.AlreadyExists };
        db.ByPath["acevo_playerNames/ace-driver"] = new FirestoreDocument(
            "ace-driver", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ownerUid"] = "someone-else",
            });

        Assert.Equal(NameClaimResult.Taken, await Registry(db).ClaimAsync("Ace Driver"));
    }

    [Fact]
    public async Task Claim_OwnName_ReportsOwned()
    {
        var db = new FakeFirestoreDatabase { NextCreateResult = FirestoreCreateResult.AlreadyExists };
        db.ByPath["acevo_playerNames/ace-driver"] = new FirestoreDocument(
            "ace-driver", new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ownerUid"] = "uid-1",
            });

        Assert.Equal(NameClaimResult.Owned, await Registry(db).ClaimAsync("Ace Driver"));
    }

    [Fact]
    public async Task Claim_FailedTransport_ReportsUnavailable()
    {
        var db = new FakeFirestoreDatabase { NextCreateResult = FirestoreCreateResult.Failed };

        Assert.Equal(NameClaimResult.Unavailable, await Registry(db).ClaimAsync("Ace Driver"));
    }

    [Fact]
    public async Task Claim_InvalidName_StopsBeforeTheNetwork()
    {
        var db = new FakeFirestoreDatabase();

        Assert.Equal(NameClaimResult.Invalid, await Registry(db).ClaimAsync("!"));
        Assert.Empty(db.Sets);
    }

    [Fact]
    public async Task Claim_NoUid_ReportsUnavailable()
    {
        var db = new FakeFirestoreDatabase();
        var registry = new FirestorePlayerNameRegistry(
            db, _ => Task.FromResult<string?>(null), new FakeClock(), new NullLogger());

        Assert.Equal(NameClaimResult.Unavailable, await registry.ClaimAsync("Ace Driver"));
        Assert.Empty(db.Sets);
    }
}
