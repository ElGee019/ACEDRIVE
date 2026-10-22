using System.Net.Http;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Anonymous Firebase auth: sign-in, session persistence, token cache.</summary>
public sealed class FirebaseSessionTests
{
    [Fact]
    public async Task Initialize_SignsInAnonymouslyAndPersistsTheRefreshToken()
    {
        var handler = new FakeAuthHandler();
        using HttpClient http = new(handler);
        var store = new FakeSessionStore();
        using var sessionWithStore = new FirebaseSession(
            new FirebaseOptions("project", "key", "bucket"), http, store, new NullLogger());

        Assert.True(await sessionWithStore.InitializeAsync(CancellationToken.None));
        Assert.Equal("uid-1", sessionWithStore.Uid);
        Assert.True(sessionWithStore.IsReady);
        Assert.NotNull(store.State);
        Assert.Equal("refresh", store.State!.RefreshToken);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetIdToken_ReturnsTheCachedTokenWithoutASecondCall()
    {
        var handler = new FakeAuthHandler();
        using HttpClient http = new(handler);
        using var session = new FirebaseSession(
            new FirebaseOptions("project", "key", "bucket"),
            http, new FakeSessionStore(), new NullLogger());

        Assert.Equal("token", await session.GetIdTokenAsync(CancellationToken.None));
        Assert.Equal("token", await session.GetIdTokenAsync(CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Dispose_ThenInitialize_AnswersFalse()
    {
        var handler = new FakeAuthHandler();
        using HttpClient http = new(handler);
        var session = new FirebaseSession(
            new FirebaseOptions("project", "key", "bucket"),
            http, new FakeSessionStore(), new NullLogger());

        session.Dispose();

        Assert.False(await session.InitializeAsync(CancellationToken.None));
    }
}
