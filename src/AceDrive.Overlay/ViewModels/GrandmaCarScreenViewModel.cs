using System.Windows.Input;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// Onboarding page 3: the car grandma hands over (PORTPLAN Career Start item
/// 3). One offer, drawn as a picture instead of a button — the pick is fixed,
/// so the page only has to ask the player to change into it and then confirm.
/// Mouse-only by companion parity (see <see cref="BrandScreenViewModel"/>).
/// </summary>
public sealed class GrandmaCarScreenViewModel : ViewModelBase
{
    private readonly IScreenController _controller;

    public GrandmaCarScreenViewModel(IScreenController controller, GrandmaCar car)
    {
        _controller = controller;
        Car = car;
        ContinueCommand = new RelayCommand(() => _controller.ChooseCar(car.Id));
    }

    /// <summary>Grandma's offer: the one car the introduction asks for.</summary>
    public GrandmaCar Car { get; }

    /// <summary>
    /// False when the data ships no render for the offer; the page then shows
    /// the instruction line alone rather than an empty frame.
    /// </summary>
    public bool HasImage => Car.ImagePath is not null;

    public ICommand ContinueCommand { get; }
}
