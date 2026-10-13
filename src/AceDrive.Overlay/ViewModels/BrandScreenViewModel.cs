using System.Windows.Input;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Onboarding page 1: favorite brand grid (PORTPLAN Career Start item 2).
/// Mouse-only by companion parity: exposing brand names as voice labels made
/// the wake utterance's final result ("hey toyota") fire the Toyota button
/// the instant the page appeared. Only the dialogue's Continue speaks.
/// </summary>
public sealed class BrandScreenViewModel : ViewModelBase
{
    private readonly IScreenController _controller;

    public BrandScreenViewModel(IScreenController controller, IReadOnlyList<string> brands)
    {
        _controller = controller;
        Brands = brands;
        ChooseBrandCommand = new ParameterizedCommand<string>(b =>
        {
            if (b is not null) _controller.ChooseBrand(b);
        });
    }

    public IReadOnlyList<string> Brands { get; }

    public ICommand ChooseBrandCommand { get; }
}
