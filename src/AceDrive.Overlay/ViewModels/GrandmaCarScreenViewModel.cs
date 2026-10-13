using System.Windows.Input;
using AceDrive.Core;

namespace AceDrive.Overlay.ViewModels;

/// <summary>One car button: label plus optional thumbnail.</summary>
public sealed record CarOption(GrandmaCar Car, string Label, bool HasImage);

/// <summary>
/// Onboarding page 2: which of grandma's cars she drives (Career Start item 3).
/// Mouse-only by companion parity (see BrandScreenViewModel).
/// </summary>
public sealed class GrandmaCarScreenViewModel : ViewModelBase
{
    private readonly IScreenController _controller;

    public GrandmaCarScreenViewModel(IScreenController controller, IReadOnlyList<GrandmaCar> cars)
    {
        _controller = controller;
        Options = cars.Select(c => new CarOption(c, Label(c), c.ImagePath is not null)).ToArray();
        SelectCarCommand = new ParameterizedCommand<CarOption>(o =>
        {
            if (o is not null) _controller.ChooseCar(o.Car.Id);
        });
    }

    public IReadOnlyList<CarOption> Options { get; }

    public ICommand SelectCarCommand { get; }

    // Button text: "YEAR name"; tolerates entries without a year.
    internal static string Label(GrandmaCar car) =>
        car.Year > 0 ? $"{car.Year} {car.Name}" : car.Name;
}
