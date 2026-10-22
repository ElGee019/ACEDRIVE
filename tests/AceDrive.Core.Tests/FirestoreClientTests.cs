using System.Net;
using System.Net.Http;
using System.Text.Json;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Firestore REST transport: URLs, JSON document names and conflicts.</summary>
public sealed class FirestoreClientTests
{
    private static async Task<(FirestoreClient Client, HttpClient Http)> CreateAsync(
        FakeFirestoreHandler handler)
    {
        HttpClient http = new(handler);
        var session = new FirebaseSession(
            new FirebaseOptions("project", "key", "bucket"),
            http, new FakeSessionStore(), new NullLogger());
        await session.InitializeAsync(CancellationToken.None);
        var client = new FirestoreClient(
            new FirebaseOptions("project", "key", "bucket"), session, http, new NullLogger());
        return (client, http);
    }

    [Fact]
    public async Task SetFields_EscapesTheUrlSegmentAndBuildsTheUpdateMask()
    {
        var handler = new FakeFirestoreHandler();
        var (client, http) = await CreateAsync(handler);
        using (http)
        {
            Assert.True(await client.SetFieldsAsync(
                "acevo_cars/ks_car#preset",
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["km"] = 8L },
                CancellationToken.None));
        }

        Assert.Contains("ks_car%23preset", handler.Uris[^1]);
        Assert.Contains("updateMask.fieldPaths=km", handler.Uris[^1]);
        using JsonDocument body = JsonDocument.Parse(handler.Bodies[^1]);
        Assert.Equal(
            "8",
            body.RootElement.GetProperty("fields").GetProperty("km")
                .GetProperty("integerValue").GetString());
    }

    [Fact]
    public async Task Create_UsesTheRawDocumentNameInJson()
    {
        var handler = new FakeFirestoreHandler();
        var (client, http) = await CreateAsync(handler);
        using (http)
        {
            Assert.Equal(FirestoreCreateResult.Created, await client.CreateAsync(
                "acevo_roadTestStandings/uid-1|ks_car#preset",
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["v"] = 2 },
                CancellationToken.None));
        }

        using JsonDocument body = JsonDocument.Parse(handler.Bodies[^1]);
        string name = body.RootElement.GetProperty("writes")[0]
            .GetProperty("update").GetProperty("name").GetString()!;
        Assert.Contains("acevo_roadTestStandings/uid-1|ks_car#preset", name);
        Assert.DoesNotContain("%23", name);
    }

    [Fact]
    public async Task Increment_SendsTheServerSideTransform()
    {
        var handler = new FakeFirestoreHandler();
        var (client, http) = await CreateAsync(handler);
        using (http)
        {
            Assert.True(await client.IncrementAsync(
                "acevo_cars/ks_car#preset",
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["lastUpdatedAt"] = "2026-10-22T12:00:00Z",
                },
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["totalKmDriven"] = 8,
                },
                CancellationToken.None));
        }

        using JsonDocument body = JsonDocument.Parse(handler.Bodies[^1]);
        JsonElement write = body.RootElement.GetProperty("writes")[0];
        Assert.Contains("acevo_cars/ks_car#preset", write
            .GetProperty("update").GetProperty("name").GetString());
        Assert.Equal("lastUpdatedAt", write
            .GetProperty("updateMask").GetProperty("fieldPaths")[0].GetString());
        JsonElement transform = write.GetProperty("updateTransforms")[0];
        Assert.Equal("totalKmDriven", transform.GetProperty("fieldPath").GetString());
        Assert.Equal("8", transform.GetProperty("increment")
            .GetProperty("integerValue").GetString());
    }

    [Fact]
    public async Task Create_Conflict_ReportsAlreadyExists()
    {
        var handler = new FakeFirestoreHandler
        {
            Status = HttpStatusCode.Conflict,
            Body = """{"error":{"code":409,"message":"ALREADY_EXISTS"}}""",
        };
        var (client, http) = await CreateAsync(handler);
        using (http)
        {
            Assert.Equal(FirestoreCreateResult.AlreadyExists, await client.CreateAsync(
                "acevo_playerNames/ace",
                new Dictionary<string, object?>(StringComparer.Ordinal) { ["ownerUid"] = "uid-1" },
                CancellationToken.None));
        }
    }

    [Fact]
    public async Task List_FollowsThePageToken()
    {
        var handler = new FakeFirestoreHandler();
        handler.Responses.Enqueue("""
            {
              "documents": [
                { "name": "projects/p/databases/(default)/documents/acevo_players/a",
                  "fields": { "xp": { "doubleValue": 1 } } }
              ],
              "nextPageToken": "next"
            }
            """);
        handler.Responses.Enqueue("""
            {
              "documents": [
                { "name": "projects/p/databases/(default)/documents/acevo_players/b",
                  "fields": { "xp": { "doubleValue": 2 } } }
              ]
            }
            """);
        var (client, http) = await CreateAsync(handler);
        IReadOnlyList<FirestoreDocument>? rows;
        using (http)
        {
            rows = await client.ListAsync("acevo_players", CancellationToken.None);
        }

        Assert.NotNull(rows);
        Assert.Equal(["a", "b"], rows!.Select(row => row.Id));
        Assert.Equal(2, handler.Uris.Count(uri =>
            uri.Contains("acevo_players", StringComparison.Ordinal)));
    }
}
