using System.Net;
using System.Text;

namespace AceDrive.Core.Tests;

/// <summary>Answers a Firebase anonymous sign-up with a fixed session.</summary>
public sealed class FakeAuthHandler : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        const string json = """
            {"idToken":"token","refreshToken":"refresh","localId":"uid-1","expiresIn":"3600"}
            """;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }
}
