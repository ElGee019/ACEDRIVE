using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Firestore REST transport. Anonymous-auth Firebase ID tokens ride the
/// bearer header, so security rules see the same <c>request.auth</c> the
/// Unity SDK gives them. Every operation degrades to null/false instead of
/// throwing; retries are bounded and never run in the gameplay loop.
/// </summary>
public sealed class FirestoreClient : IFirestoreDatabase
{
    private const int MaxRetries = 3;
    private const int PageSize = 300;
    private const int BatchLimit = 500;

    private readonly FirebaseOptions _options;
    private readonly FirebaseSession _session;
    private readonly HttpClient _http;
    private readonly ILog _log;
    private readonly string _baseUrl;
    private bool _permissionsHintLogged;

    public FirestoreClient(
        FirebaseOptions options, FirebaseSession session, HttpClient http, ILog log)
    {
        _options = options;
        _session = session;
        _http = http;
        _log = log;
        _baseUrl = "https://firestore.googleapis.com/v1/projects/"
            + Uri.EscapeDataString(options.ProjectId)
            + "/databases/(default)/documents";
    }

    public async Task<IReadOnlyList<FirestoreDocument>?> ListAsync(
        string collection, CancellationToken cancellationToken)
    {
        List<FirestoreDocument> documents = [];
        string? pageToken = null;
        do
        {
            string url = $"{_baseUrl}/{EscapePath(collection)}?pageSize={PageSize}";
            if (!string.IsNullOrEmpty(pageToken))
            {
                url += "&pageToken=" + Uri.EscapeDataString(pageToken);
            }

            using FirestoreResponse? response = await SendAsync(
                HttpMethod.Get, url, null, cancellationToken).ConfigureAwait(false);
            if (response is not { Status: HttpStatusCode.OK, Body: not null })
            {
                return null;
            }

            JsonElement root = response.Body.RootElement;
            if (root.TryGetProperty("documents", out JsonElement rows)
                && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement row in rows.EnumerateArray())
                {
                    FirestoreDocument? doc = FirestoreJson.ReadDocument(row);
                    if (doc is not null)
                    {
                        documents.Add(doc);
                    }
                }
            }

            pageToken = root.TryGetProperty("nextPageToken", out JsonElement token)
                && token.ValueKind == JsonValueKind.String
                ? token.GetString()
                : null;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return documents;
    }

    public async Task<FirestoreDocument?> GetAsync(
        string documentPath, CancellationToken cancellationToken)
    {
        using FirestoreResponse? response = await SendAsync(
            HttpMethod.Get, DocUrl(documentPath), null, cancellationToken)
            .ConfigureAwait(false);
        if (response is not { Status: HttpStatusCode.OK, Body: not null })
        {
            return null;
        }
        return FirestoreJson.ReadDocument(response.Body.RootElement);
    }

    public async Task<bool> SetFieldsAsync(
        string documentPath,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken)
    {
        if (fields.Count == 0)
        {
            return true;
        }

        string url = DocUrl(documentPath) + UpdateMask(fields.Keys);
        JsonObject body = new() { ["fields"] = FirestoreJson.WriteFields(fields) };
        using FirestoreResponse? response = await SendAsync(
            HttpMethod.Patch, url, body, cancellationToken).ConfigureAwait(false);
        return response is { Status: HttpStatusCode.OK };
    }

    public async Task<bool> IncrementAsync(
        string documentPath,
        IReadOnlyDictionary<string, object?> fields,
        IReadOnlyDictionary<string, long> increments,
        CancellationToken cancellationToken)
    {
        if (increments.Count == 0)
        {
            return true;
        }

        JsonArray transforms = [];
        foreach (KeyValuePair<string, long> increment in increments)
        {
            if (increment.Value == 0)
            {
                continue;
            }
            transforms.Add(new JsonObject
            {
                ["fieldPath"] = increment.Key,
                ["increment"] = new JsonObject
                {
                    ["integerValue"] = increment.Value.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                },
            });
        }
        if (transforms.Count == 0)
        {
            return true;
        }

        JsonObject write = [];
        if (fields.Count > 0)
        {
            write["update"] = new JsonObject
            {
                ["name"] = FullName(documentPath),
                ["fields"] = FirestoreJson.WriteFields(fields),
            };
            write["updateMask"] = new JsonObject
            {
                ["fieldPaths"] = FieldPaths(fields.Keys),
            };
            write["updateTransforms"] = transforms;
        }
        else
        {
            write["transform"] = new JsonObject
            {
                ["document"] = FullName(documentPath),
                ["fieldTransforms"] = transforms,
            };
        }

        using FirestoreResponse? response = await CommitAsync(write, cancellationToken)
            .ConfigureAwait(false);
        return response is { Status: HttpStatusCode.OK };
    }

    public async Task<FirestoreCreateResult> CreateAsync(
        string documentPath,
        IReadOnlyDictionary<string, object?> fields,
        CancellationToken cancellationToken)
    {
        JsonObject write = new()
        {
            ["update"] = new JsonObject
            {
                ["name"] = FullName(documentPath),
                ["fields"] = FirestoreJson.WriteFields(fields),
            },
            ["currentDocument"] = new JsonObject { ["exists"] = false },
        };
        using FirestoreResponse? response = await CommitAsync(write, cancellationToken)
            .ConfigureAwait(false);
        return response?.Status switch
        {
            HttpStatusCode.OK => FirestoreCreateResult.Created,
            HttpStatusCode.Conflict => FirestoreCreateResult.AlreadyExists,
            _ => FirestoreCreateResult.Failed,
        };
    }

    public async Task<IReadOnlyList<FirestoreDocument>?> QueryEqualsAsync(
        string collection, string field, string value,
        CancellationToken cancellationToken)
    {
        JsonObject body = new()
        {
            ["structuredQuery"] = new JsonObject
            {
                ["from"] = new JsonArray
                {
                    new JsonObject { ["collectionId"] = collection },
                },
                ["where"] = new JsonObject
                {
                    ["fieldFilter"] = new JsonObject
                    {
                        ["field"] = new JsonObject { ["fieldPath"] = field },
                        ["op"] = "EQUAL",
                        ["value"] = new JsonObject { ["stringValue"] = value },
                    },
                },
            },
        };

        string url = _baseUrl + ":runQuery";
        using FirestoreResponse? response = await SendAsync(
            HttpMethod.Post, url, body, cancellationToken).ConfigureAwait(false);
        if (response is not { Status: HttpStatusCode.OK, Body: not null })
        {
            return null;
        }

        List<FirestoreDocument> documents = [];
        if (response.Body.RootElement.ValueKind != JsonValueKind.Array)
        {
            return documents;
        }
        foreach (JsonElement row in response.Body.RootElement.EnumerateArray())
        {
            if (row.TryGetProperty("document", out JsonElement document))
            {
                FirestoreDocument? doc = FirestoreJson.ReadDocument(document);
                if (doc is not null)
                {
                    documents.Add(doc);
                }
            }
        }
        return documents;
    }

    public async Task<bool> BatchDeleteAsync(
        IReadOnlyList<string> documentPaths, CancellationToken cancellationToken)
    {
        for (int offset = 0; offset < documentPaths.Count; offset += BatchLimit)
        {
            JsonArray writes = [];
            int end = Math.Min(offset + BatchLimit, documentPaths.Count);
            for (int i = offset; i < end; i++)
            {
                writes.Add(new JsonObject { ["delete"] = FullName(documentPaths[i]) });
            }

            JsonObject body = new() { ["writes"] = writes };
            using FirestoreResponse? response = await SendAsync(
                HttpMethod.Post, _baseUrl + ":commit", body, cancellationToken)
                .ConfigureAwait(false);
            if (response is not { Status: HttpStatusCode.OK })
            {
                return false;
            }
        }
        return true;
    }

    private async Task<FirestoreResponse?> CommitAsync(
        JsonObject write, CancellationToken cancellationToken)
    {
        JsonObject body = new() { ["writes"] = new JsonArray { write } };
        return await SendAsync(
            HttpMethod.Post, _baseUrl + ":commit", body, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<FirestoreResponse?> SendAsync(
        HttpMethod method, string url, JsonNode? body, CancellationToken cancellationToken)
    {
        bool refreshed = false;
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            string? token;
            try
            {
                token = await _session.GetIdTokenAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            using HttpRequestMessage request = new(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (body is not null)
            {
                request.Content = new StringContent(
                    body.ToJsonString(), Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(
                    request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                if (attempt == MaxRetries)
                {
                    _log.LogWarning($"firestore: {method} failed: {ex.Message}");
                    return null;
                }
                await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == MaxRetries)
                {
                    _log.LogWarning("firestore: request timed out");
                    return null;
                }
                await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                continue;
            }
            catch (OperationCanceledException)
            {
                // Caller/shutdown cancellation: no retry, no log noise.
                return null;
            }
            catch (ObjectDisposedException)
            {
                // Shutdown disposed the shared client mid-request.
                return null;
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized && !refreshed)
                {
                    refreshed = true;
                    _session.InvalidateToken();
                    continue;
                }
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    LogPermissionsHint();
                    return null;
                }
                if (IsTransient(response.StatusCode) && attempt < MaxRetries)
                {
                    await DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                JsonDocument? parsed = await ReadBodyAsync(response, cancellationToken)
                    .ConfigureAwait(false);
                if (response.StatusCode is not HttpStatusCode.OK
                    && response.StatusCode is not HttpStatusCode.NoContent
                    && response.StatusCode is not HttpStatusCode.NotFound
                    && response.StatusCode is not HttpStatusCode.Conflict)
                {
                    _log.LogWarning(
                        $"firestore: {method} returned {(int)response.StatusCode}");
                }
                return new FirestoreResponse(response.StatusCode, parsed);
            }
        }
        return null;
    }

    private async Task<JsonDocument?> ReadBodyAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            _log.LogWarning($"firestore: response read failed: {ex.Message}");
            return null;
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException ex)
        {
            _log.LogWarning($"firestore: malformed response: {ex.Message}");
            return null;
        }
    }

    private static async Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2 * attempt), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Shutdown: the caller is about to see the cancelled token.
        }
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private void LogPermissionsHint()
    {
        if (_permissionsHintLogged) return;
        _permissionsHintLogged = true;
        _log.LogWarning(
            "firestore: permission denied; deploy firestore.rules with the acevo_* collections");
    }

    private string DocUrl(string documentPath) =>
        $"{_baseUrl}/{EscapePath(documentPath)}";

    // JSON document names are resource names, not URLs: escaping here would
    // turn a variant key's '#' into a literal '%23' doc id. Only the HTTP
    // path (DocUrl) percent-encodes segments.
    private string FullName(string documentPath) =>
        $"projects/{_options.ProjectId}/databases/(default)/documents/{documentPath}";

    private static string EscapePath(string path)
    {
        StringBuilder result = new(path.Length);
        int start = 0;
        for (int i = 0; i <= path.Length; i++)
        {
            if (i < path.Length && path[i] != '/')
            {
                continue;
            }
            if (i > start)
            {
                result.Append(Uri.EscapeDataString(path[start..i]));
            }
            if (i < path.Length)
            {
                result.Append('/');
            }
            start = i + 1;
        }
        return result.ToString();
    }

    private static string UpdateMask(IEnumerable<string> fields)
    {
        StringBuilder result = new();
        foreach (string field in fields)
        {
            result.Append(result.Length == 0 ? "?" : "&");
            result.Append("updateMask.fieldPaths=");
            result.Append(Uri.EscapeDataString(field));
        }
        return result.ToString();
    }

    private static JsonArray FieldPaths(IEnumerable<string> fields)
    {
        JsonArray result = [];
        foreach (string field in fields)
        {
            result.Add(field);
        }
        return result;
    }

    private sealed class FirestoreResponse : IDisposable
    {
        public FirestoreResponse(HttpStatusCode status, JsonDocument? body)
        {
            Status = status;
            Body = body;
        }

        public HttpStatusCode Status { get; }
        public JsonDocument? Body { get; }

        public void Dispose() => Body?.Dispose();
    }
}
