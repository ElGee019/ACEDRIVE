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
    /// Puts the companion on screen. The introduction keeps it hidden until
    /// the birthday dialogue hands the errand over, so the opening pages are
    /// not covered by live mileage and position.
    /// </summary>
    void Reveal();

    /// <summary>
    /// One poll, live or not. <paramref name="sample"/> is null when EVO is
    /// absent or the physics read was torn; <paramref name="odometerKm"/> is
    /// the driven car's persistent mileage, null when no car is owned.
    /// <paramref name="wrongCar"/> is true when the live car is not the
    /// garage's current car; the HUD then replaces the errand line with the
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
    /// Pins the active errand's destination on the HUD, or clears it with
    /// null. Called when the birthday dialogue hands the errand over, never
    /// per poll.
    /// </summary>
    void SetDestination(PointOfInterest? destination);

    /// <summary>
    /// True while the car is parked on the errand house; the ENTER button is
    /// up and voice may fire it.
    /// </summary>
    bool AtDestination { get; }

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
}
