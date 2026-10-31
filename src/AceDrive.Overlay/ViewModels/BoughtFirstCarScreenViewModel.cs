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
///
/// Both buttons answer the mic as well as a click: this page is the only way out of
/// a purchase, and a player who just bought a car has no reason to reach for the
/// mouse. The taps act at once like the settings page's continue — one choice ends
/// the page, so there is no second tap for the beat's glow to protect.
/// </summary>
public sealed class BoughtFirstCarScreenViewModel : ViewModelBase, IVoiceScreen
{
    public BoughtFirstCarScreenViewModel(
        CarSpec car, bool secondCar, ICommand driveCommand, ICommand keepCommand)
    {
        CarLine = CarTitles.Of(car);
        SummaryLine = secondCar ? Strings.Get("BoughtSecondCar") : string.Empty;
        DriveCommand = driveCommand;
        KeepCommand = keepCommand;
        VoiceActions = new Dictionary<string, Action>
        {
            [Strings.Get("BoughtDrive")] = () => DriveCommand.Execute(null),
            [Strings.Get("BoughtKeep")] = () => KeepCommand.Execute(null),
        };
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

    /// <summary>Both buttons answer their printed labels.</summary>
    public IReadOnlyDictionary<string, Action> VoiceActions { get; }
}
