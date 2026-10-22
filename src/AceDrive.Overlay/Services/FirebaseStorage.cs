using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// Firebase Storage download for the optional cloud car sprites. The
/// gameplay already has local thumbnails, so this is service-level parity
/// for the shared Unity bucket: no caller until a cloud-sprite screen lands.
/// Never throws.
/// </summary>
public sealed class FirebaseStorage
{
    private const int MaxRetries = 2;

    private readonly FirebaseOptions _options;
    private readonly FirebaseSession _session;
    private readonly HttpClient _http;
    private readonly ILog _log;

    public FirebaseStorage(
        FirebaseOptions options, FirebaseSession session, HttpClient http, ILog log)
    {
        _options = options;
        _session = session;
        _http = http;
        _log = log;
    }

    /// <summary>The PNG bytes at the object path, or null when unavailable.</summary>
    public async Task<byte[]?> DownloadAsync(
        string objectPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectPath))
        {
            return null;
        }

        string url = "https://firebasestorage.googleapis.com/v0/b/"
            + Uri.EscapeDataString(_options.StorageBucket)
            + "/o/" + Uri.EscapeDataString(objectPath) + "?alt=media";

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

            using HttpRequestMessage request = new(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
                    _log.LogWarning($"firebase storage: {ex.Message}");
                    return null;
                }
                continue;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == MaxRetries)
                {
                    _log.LogWarning("firebase storage: request timed out");
                    return null;
                }
                continue;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }

            using (response)
            {
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    try
                    {
                        return await response.Content.ReadAsByteArrayAsync(cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (HttpRequestException ex)
                    {
                        _log.LogWarning($"firebase storage: read failed: {ex.Message}");
                        return null;
                    }
                }

                if (response.StatusCode is HttpStatusCode.TooManyRequests
                    or HttpStatusCode.InternalServerError
                    or HttpStatusCode.BadGateway
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.GatewayTimeout)
                {
                    continue;
                }

                _log.LogWarning(
                    $"firebase storage: {response.StatusCode} for {objectPath}");
                return null;
            }
        }
        return null;
    }
}
