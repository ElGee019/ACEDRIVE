using AceDrive.Core;

namespace AceDrive.Overlay.Services;

/// <summary>Onboarding progress behind an interface so views never touch the disk.</summary>
public interface IOnboardingStore
{
    OnboardingState Load();
    void Save(OnboardingState state);
}
