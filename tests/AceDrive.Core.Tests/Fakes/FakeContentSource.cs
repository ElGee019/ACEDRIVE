using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Core.Tests;

internal sealed class FakeContentSource : IContentSource
{
    public FakeContentSource(OnboardingContent content) => Content = content;

    public OnboardingContent Content { get; set; }
}
