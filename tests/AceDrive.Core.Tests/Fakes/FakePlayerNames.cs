using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

/// <summary>Scriptable name registry, with an optional gate for the busy state.</summary>
public sealed class FakePlayerNames : IPlayerNameRegistry
{
    public NameClaimResult Result { get; set; } = NameClaimResult.Claimed;
    public TaskCompletionSource<bool>? Gate { get; set; }
    public List<string> Claims { get; } = [];

    public async Task<NameClaimResult> ClaimAsync(
        string displayName, CancellationToken cancellationToken = default)
    {
        Claims.Add(displayName);
        if (Gate is not null)
        {
            await Gate.Task.ConfigureAwait(false);
        }
        return Result;
    }
}
