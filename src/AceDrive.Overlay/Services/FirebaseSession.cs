using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Anonymous Firebase Authentication. The first launch creates an anonymous
/// user and persists its refresh token; later launches refresh that same
/// identity so the world-board doc survives restarts. Definitive account
/// errors mint a fresh anonymous user, while transient/network failures keep
/// the token and simply leave the online layer unavailable this session.
/// Never throws across the boundary.
/// </summary>
public sealed class FirebaseSession : IDisposable
{
    private const string SignUpUrl =
        "https://identitytoolkit.googleapis.com/v1/accounts:signUp?key=";
    private const string RefreshUrl =
        "https://securetoken.googleapis.com/v1/token?key=";

    private readonly FirebaseOptions _options;
    private readonly HttpClient _http;
    private readonly IFirebaseSessionStore _store;
    private readonly ILog _log;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _idToken;
    private DateTime _expiresAtUtc;
    private DateTime _retryAfterUtc = DateTime.MinValue;
    private bool _disposed;

    public FirebaseSession(
        FirebaseOptions options, HttpClient http, IFirebaseSessionStore store, ILog log)
    {
        _options = options;
        _http = http;
        _store = store;
        _log = log;
    }

    public string? Uid { get; private set; }
    public string? LastError { get; private set; }
    public bool IsReady =>
        Uid is { Length: > 0 }
        && _idToken is { Length: > 0 }
        && _expiresAtUtc > DateTime.UtcNow;

    /// <summary>
    /// Returns a valid Firebase ID token, initializing/refreshing as needed.
    /// Null means the online layer is unavailable; callers degrade silently.
    /// </summary>
    public async Task<string?> GetIdTokenAsync(CancellationToken cancellationToken)
    {
        if (IsReady && _expiresAtUtc > DateTime.UtcNow.AddMinutes(2))
        {
            return _idToken;
        }
        return await InitializeAsync(cancellationToken).ConfigureAwait(false) ? _idToken : null;
    }

    /// <summary>
    /// Drops the cached ID token so the next request refreshes it. Used after
    /// a 401 from Firestore, where the server rejected a token our clock
    /// still considered valid.
    /// </summary>
    public void InvalidateToken()
    {
        _idToken = null;
        _expiresAtUtc = DateTime.MinValue;
    }

    /// <summary>Signs in anonymously or resumes the persisted anonymous user.</summary>
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken)
    {
        if (_disposed) return false;
        // A broken key/disabled anonymous auth would otherwise be retried by
        // every push and board load; back off briefly after a failed attempt.
        if (!IsReady && DateTime.UtcNow < _retryAfterUtc) return false;
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or OperationCanceledException)
        {
            return false;
        }

        try
        {
            if (IsReady && _expiresAtUtc > DateTime.UtcNow.AddMinutes(2))
            {
                return true;
            }

            FirebaseSessionState? saved = _store.Load();
            if (saved is not null)
            {
                RefreshOutcome outcome = await TryRefreshAsync(saved, cancellationToken)
                    .ConfigureAwait(false);
                if (outcome == RefreshOutcome.Success)
                {
                    return true;
                }
                if (outcome == RefreshOutcome.Transient)
                {
                    return false;
                }
                _store.Clear();
                Uid = null;
                _idToken = null;
                _expiresAtUtc = DateTime.MinValue;
            }

            return await SignUpAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            LastError = ex.Message;
            _log.LogWarning($"firebase auth: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            LastError = ex.Message;
            _log.LogWarning("firebase auth: request timed out");
            return false;
        }
        catch (JsonException ex)
        {
            LastError = ex.Message;
            _log.LogWarning($"firebase auth: malformed response: {ex.Message}");
            return false;
        }
        catch (OperationCanceledException)
        {
            // Shutdown cancelled the sign-in; not an error worth logging.
            return false;
        }
        catch (InvalidOperationException ex)
        {
            LastError = ex.Message;
            _log.LogWarning($"firebase auth: {ex.Message}");
            return false;
        }
        finally
        {
            _gate.Release();
            if (!IsReady)
            {
                _retryAfterUtc = DateTime.UtcNow.AddSeconds(30);
            }
        }
    }

    private async Task<RefreshOutcome> TryRefreshAsync(
        FirebaseSessionState saved, CancellationToken cancellationToken)
    {
        using FormUrlEncodedContent form = new(
        [
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", saved.RefreshToken),
        ]);
        using HttpResponseMessage response = await _http.PostAsync(
            RefreshUrl + Uri.EscapeDataString(_options.ApiKey), form, cancellationToken)
            .ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            return ClassifyRefreshFailure(body);
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        string? idToken = ReadString(root, "id_token");
        string? refreshToken = ReadString(root, "refresh_token");
        string? uid = ReadString(root, "user_id");
        string expires = ReadString(root, "expires_in") ?? "3600";
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(refreshToken)
            || string.IsNullOrEmpty(uid))
        {
            LastError = "refresh returned an incomplete session";
            _log.LogWarning($"firebase auth: {LastError}");
            return RefreshOutcome.Transient;
        }

        StoreSession(uid, idToken, refreshToken, expires, "resumed");
        return RefreshOutcome.Success;
    }

    private RefreshOutcome ClassifyRefreshFailure(string body)
    {
        string code = ErrorCode(body);
        LastError = code.Length > 0 ? code : "refresh failed";
        if (code is "INVALID_REFRESH_TOKEN" or "TOKEN_EXPIRED"
            or "USER_NOT_FOUND" or "USER_DISABLED")
        {
            _log.LogWarning($"firebase auth: persisted session invalid ({code}); signing in again");
            return RefreshOutcome.Invalid;
        }
        _log.LogWarning($"firebase auth: refresh failed ({LastError})");
        return RefreshOutcome.Transient;
    }

    private async Task<bool> SignUpAsync(CancellationToken cancellationToken)
    {
        using StringContent content = new(
            "{\"returnSecureToken\":true}", Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await _http.PostAsync(
            SignUpUrl + Uri.EscapeDataString(_options.ApiKey), content, cancellationToken)
            .ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            LastError = ErrorCode(body);
            _log.LogWarning($"firebase auth: anonymous sign-in failed ({LastError})");
            return false;
        }

        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        string? idToken = ReadString(root, "idToken");
        string? refreshToken = ReadString(root, "refreshToken");
        string? uid = ReadString(root, "localId");
        string expires = ReadString(root, "expiresIn") ?? "3600";
        if (string.IsNullOrEmpty(idToken) || string.IsNullOrEmpty(refreshToken)
            || string.IsNullOrEmpty(uid))
        {
            LastError = "sign-in returned an incomplete session";
            _log.LogWarning($"firebase auth: {LastError}");
            return false;
        }

        StoreSession(uid, idToken, refreshToken, expires, "signed in");
        return true;
    }

    private void StoreSession(
        string uid, string idToken, string refreshToken, string expiresIn, string action)
    {
        Uid = uid;
        _idToken = idToken;
        int seconds = int.TryParse(expiresIn, NumberStyles.Integer, CultureInfo.InvariantCulture,
            out int parsed) ? Math.Max(60, parsed) : 3600;
        _expiresAtUtc = DateTime.UtcNow.AddSeconds(seconds);
        _store.Save(new FirebaseSessionState(uid, refreshToken));
        LastError = null;
        _log.LogInfo($"firebase: {action}, uid={uid}");
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value)
            ? value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString()
            : null;

    private static string ErrorCode(string body)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out JsonElement error)
                && error.TryGetProperty("message", out JsonElement message))
            {
                return message.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            // A proxy/login page answers HTML; the status already says "failed".
        }
        return string.Empty;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _gate.Dispose();
    }

    private enum RefreshOutcome
    {
        Success,
        Invalid,
        Transient,
    }
}
