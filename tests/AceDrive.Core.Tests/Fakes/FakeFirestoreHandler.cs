using System.Net;
using System.Text;

namespace AceDrive.Core.Tests;

/// <summary>
/// Records REST requests and answers the Firebase auth endpoint plus a
/// configurable Firestore response, so the transport can be tested without a
/// network.
/// </summary>
public sealed class FakeFirestoreHandler : HttpMessageHandler
{
    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
    public string Body { get; set; } = "{}";

    /// <summary>Optional scripted responses, consumed before <see cref="Body"/>.</summary>
    public Queue<string> Responses { get; } = new();
    public List<string> Uris { get; } = [];
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string uri = request.RequestUri?.ToString() ?? string.Empty;
        Uris.Add(uri);
        Bodies.Add(request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken));

        if (request.RequestUri?.Host == "identitytoolkit.googleapis.com")
        {
            const string auth = """
                {"idToken":"token","refreshToken":"refresh","localId":"uid-1","expiresIn":"3600"}
                """;
            return Json(HttpStatusCode.OK, auth);
        }
        return Json(Status, Responses.Count > 0 ? Responses.Dequeue() : Body);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
}
