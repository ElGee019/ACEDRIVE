using System.Net.Http;
using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>
/// One configured Firebase connection: the shared HTTP client, the anonymous
/// session and the Firestore/Storage transports. The composition root news
/// this once and passes the consumer services down; disposing it cancels the
/// in-flight background board loads cleanly on exit.
/// </summary>
public sealed class FirebaseConnection : IDisposable
{
    private readonly HttpClient _http;
    private readonly CancellationTokenSource _stop = new();
    private readonly ILog _log;
    private bool _disposed;

    public FirebaseConnection(
        FirebaseOptions options, ILog log, HttpMessageHandler? handler = null)
    {
        _log = log;
        _http = handler is null
            ? new HttpClient()
            : new HttpClient(handler, disposeHandler: true);
        _http.Timeout = TimeSpan.FromSeconds(20);
        Options = options;
        Session = new FirebaseSession(options, _http, new JsonFirebaseSessionStore(), log);
        Database = new FirestoreClient(options, Session, _http, log);
        Storage = new FirebaseStorage(options, Session, _http, log);
    }

    public FirebaseOptions Options { get; }
    public FirebaseSession Session { get; }
    public IFirestoreDatabase Database { get; }
    public FirebaseStorage Storage { get; }
    public CancellationToken StopToken => _stop.Token;

    /// <summary>Starts the anonymous login without blocking startup.</summary>
    public void Start() =>
        SafeTask.Forget(Session.InitializeAsync(_stop.Token), _log, "firebase init");

    /// <summary>Ensures auth ran, then answers the anonymous uid (null = offline).</summary>
    public async Task<string?> UidAsync(CancellationToken cancellationToken)
    {
        await Session.GetIdTokenAsync(cancellationToken).ConfigureAwait(false);
        return Session.Uid;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stop.Cancel();
        _stop.Dispose();
        Session.Dispose();
        _http.Dispose();
    }
}
