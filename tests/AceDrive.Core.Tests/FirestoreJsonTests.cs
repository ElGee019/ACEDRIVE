using System.Globalization;
using System.Text.Json;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Firestore REST typed-value mapping.</summary>
public sealed class FirestoreJsonTests
{
    [Fact]
    public void WriteFields_EncodesTheTypedWrappers()
    {
        string json = FirestoreJson.WriteFields(new Dictionary<string, object?>
        {
            ["name"] = "Ace",
            ["xp"] = 12.5,
            ["km"] = 42L,
            ["seen"] = true,
            ["expires"] = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc),
            ["none"] = null,
        }).ToJsonString();

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement fields = doc.RootElement;
        Assert.Equal("Ace", fields.GetProperty("name").GetProperty("stringValue").GetString());
        Assert.Equal(12.5, fields.GetProperty("xp").GetProperty("doubleValue").GetDouble());
        Assert.Equal("42", fields.GetProperty("km").GetProperty("integerValue").GetString());
        Assert.True(fields.GetProperty("seen").GetProperty("booleanValue").GetBoolean());
        Assert.Equal(JsonValueKind.Null, fields.GetProperty("none").GetProperty("nullValue").ValueKind);
        Assert.Contains("2026-10-22", fields.GetProperty("expires")
            .GetProperty("timestampValue").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadDocument_DecodesFieldsAndTheDocId()
    {
        const string json = """
            {
              "name": "projects/latestdrivebs/databases/(default)/documents/acevo_players/uid-1",
              "fields": {
                "xp": { "doubleValue": 8.5 },
                "totalKmDriven": { "integerValue": "120" },
                "displayName": { "stringValue": "Ace" },
                "seen": { "booleanValue": true }
              }
            }
            """;

        using JsonDocument doc = JsonDocument.Parse(json);
        FirestoreDocument? parsed = FirestoreJson.ReadDocument(doc.RootElement);
        Assert.NotNull(parsed);
        Assert.Equal("uid-1", parsed!.Id);
        Assert.Equal(8.5, parsed.Number("xp"));
        Assert.Equal(120, parsed.Whole("totalKmDriven"));
        Assert.Equal("Ace", parsed.Text("displayName"));
        Assert.Equal(120, parsed.IntValue("totalKmDriven"));
    }

    [Fact]
    public void ReadValue_HandlesMapsAndArrays()
    {
        using JsonDocument doc = JsonDocument.Parse("""
            {
              "mapValue": {
                "fields": {
                  "keys": { "arrayValue": { "values": [
                    { "stringValue": "a" },
                    { "integerValue": "2" }
                  ] } }
                }
              }
            }
            """);

        object? value = FirestoreJson.ReadValue(doc.RootElement);
        var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(value);
        var list = Assert.IsAssignableFrom<IReadOnlyList<object?>>(map["keys"]);
        Assert.Equal("a", list[0]);
        Assert.Equal(2L, list[1]);
    }

    [Fact]
    public void DocId_IsTheLastPathSegment()
    {
        Assert.Equal("uid", FirestoreJson.DocId(
            "projects/p/databases/(default)/documents/acevo_players/uid"));
        Assert.Equal("uid|ks_car", FirestoreJson.DocId("a/uid|ks_car"));
    }
}
