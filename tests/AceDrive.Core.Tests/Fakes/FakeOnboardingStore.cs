using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeOnboardingStore : IOnboardingStore
{
    public OnboardingState State { get; private set; } = OnboardingState.Fresh;
    public OnboardingState Load() => State;
    public void Save(OnboardingState state) => State = state;
}
