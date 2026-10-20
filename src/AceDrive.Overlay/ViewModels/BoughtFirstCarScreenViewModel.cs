using System.Windows.Input;
using AceDrive.Core;
using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The post-purchase choice (PORTPLAN item 29, port of Unity BoughtFirstCar): the
/// new car is already in the garage, and this screen is the only place that decides
/// whether the player drives it now or keeps the current car. Unity shows the
/// summary block only when this was the garage's second car (grandma's starter plus
/// the first purchase); the same gate repeats here. Both buttons belong to the host,
/// which banks the outcome — the screen writes nothing.
/// </summary>
public sealed class BoughtFirstCarScreenViewModel : ViewModelBase
{
    public BoughtFirstCarScreenViewModel(
        CarSpec car, bool secondCar, ICommand driveCommand, ICommand keepCommand)
    {
        CarLine = CarTitles.Of(car);
        SummaryLine = secondCar ? Strings.Get("BoughtSecondCar") : string.Empty;
        DriveCommand = driveCommand;
        KeepCommand = keepCommand;
    }

    public string Title { get; } = Strings.Get("BoughtTitle");

    public string Body { get; } = Strings.Get("BoughtBody");

    /// <summary>The car that was just bought, all caps like every other car line.</summary>
    public string CarLine { get; }

    /// <summary>Empty on the first purchase, filled once a second car exists (Unity's showSummary gate).</summary>
    public string SummaryLine { get; }

    public bool HasSummary => SummaryLine.Length > 0;

    public ICommand DriveCommand { get; }

    public ICommand KeepCommand { get; }
}
