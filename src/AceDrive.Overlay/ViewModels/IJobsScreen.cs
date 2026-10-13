namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Marker for screens that belong to the jobs side (the main control page
/// and every job page opened from it) instead of onboarding. Keeps
/// <c>MainViewModel.IsOnboarding</c> open for new job screens without
/// lengthening a type-check chain per page (OCP).
/// </summary>
public interface IJobsScreen
{
}
