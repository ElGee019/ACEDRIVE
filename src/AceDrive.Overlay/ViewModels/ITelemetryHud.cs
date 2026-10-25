using AceDrive.Core;
using AceDrive.Overlay.Services;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// The companion HUD's view of the one 10 Hz poll. MainViewModel owns the
/// loop and pushes this snapshot; the HUD never opens shared memory itself.
/// </summary>
public interface ITelemetryHud
{
    /// <summary>
    /// Puts the companion on screen. The introduction keeps it hidden until the
    /// walk's closing car page hands the errand over, so the opening pages are
    /// not covered by live mileage and position.
    /// </summary>
    void Reveal();

    /// <summary>
    /// One poll, live or not. <paramref name="sample"/> is null when EVO is
    /// absent or the physics read was torn; <paramref name="odometerKm"/> is
    /// the driven car's persistent mileage, null when no car is owned.
    /// <paramref name="wrongCar"/> is true when the live car is not the
    /// garage's current car; the HUD then replaces the destination line with the
    /// career warning but keeps the live position and mileage.
    /// </summary>
    void Update(
        TelemetrySample? sample, double? odometerKm, bool gameRunning, bool wrongCar = false);

    /// <summary>
    /// Adopts the player's unit and language and repaints every readout at
    /// once, so the first-run page can flip them while the HUD is up.
    /// </summary>
    void ApplyPreferences(PlayerPreferences preferences);

    /// <summary>
    /// Pins the destination the story hands over on the HUD, or clears it with
    /// null. <paramref name="copy"/> is an optional story-authored set of
    /// resource keys with the same placeholders as the generic drive-to
    /// sentence. Called when a story beat moves the player on, never per poll.
    /// </summary>
    void SetDestination(PointOfInterest? destination, HudDestinationCopy? copy = null);

    /// <summary>
    /// The POI currently pinned by a story beat or the addresses page, or null.
    /// The entry path reads it so one ENTER opens whichever door the HUD is
    /// actually guiding the player to.
    /// </summary>
    PointOfInterest? CurrentDestination { get; }

    /// <summary>
    /// Pins one job leg (taxi or delivery) on the HUD or clears it with null
    /// when the job ends. Job guidance wins over a story destination; the
    /// wrong-car warning still wins over both. Called on phase changes, never
    /// per poll.
    /// </summary>
    void SetJobGuidance(HudJobGuidance? guidance);

    /// <summary>
    /// Shows a transient line under the banner (a police fine, a refusal
    /// hint). The host formats the text; the HUD expires it on the clock, so
    /// a stalled poll cannot leave it up forever.
    /// </summary>
    void ShowNotice(string text, TimeSpan duration);

    /// <summary>
    /// Presents the customer request on the companion instead of a main-window
    /// page: the ask, its options and the countdown replace the task text, and
    /// the frame grows to the page's height on the same black backdrop. The
    /// host still owns the timer and the outcome; the HUD only paints what it
    /// is handed and raises the taps back through the screen's own commands.
    /// </summary>
    void ShowRequest(CustomerRequestScreenViewModel request);

    /// <summary>Clears the customer request overlay once the outcome is in.</summary>
    void ClearRequest();

    /// <summary>
    /// Surfaces the companion's task text even while the main panel is open,
    /// so a destination pinned from a page (the addresses list) is readable at
    /// once instead of waiting for the panel to hide. The companion keeps it
    /// up until the panel next opens or the player enters the pinned door.
    /// </summary>
    void ShowTaskText();

    /// <summary>
    /// The screen the companion is presenting (the customer request), or null.
    /// The voice router taps its labels while the overlay is up, panel hidden.
    /// </summary>
    IVoiceScreen? RequestScreen { get; }

    /// <summary>
    /// True while the companion's action button is up: parked on the pinned
    /// door and not entered yet. Voice taps the button only while it is up.
    /// </summary>
    bool ShowEnterButton { get; }

    /// <summary>
    /// The host entered the pinned door (a shop interior, a story dialogue or
    /// an arrival with nothing to open). The companion's task text and action
    /// button hide until the next arrival or a new leg, so a visit does not
    /// keep inviting the ENTER it just consumed.
    /// </summary>
    void DestinationEntered();

    /// <summary>
    /// Spoken words that fire the action button while it is up: "enter"
    /// everywhere, plus "pick up"/"drop off" when the button says so. The
    /// words are English regardless of the button's label language, because
    /// the recognizer is.
    /// </summary>
    IReadOnlyList<string> EnterKeywords { get; }

    /// <summary>
    /// Voice activation: glows the button teal for the shared beat, then
    /// raises <see cref="EnterRequested"/>. A mouse click acts immediately.
    /// </summary>
    void RequestEnter();

    /// <summary>
    /// Raised when the player taps ENTER while parked on the destination.
    /// The host owns what entering means; the HUD only knows the arrival
    /// state.
    /// </summary>
    event EventHandler? EnterRequested;

    /// <summary>
    /// Raised when the player taps ENTER while parked at the pinned job door.
    /// The host decides what that confirms — the taxi pickup/drop-off or a
    /// delivery hub pickup/store drop-off; the HUD only knows which leg it
    /// painted.
    /// </summary>
    event EventHandler? JobEnterRequested;
}
